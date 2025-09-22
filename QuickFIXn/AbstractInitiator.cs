using System.Threading;
using System.Collections.Generic;
using System;

namespace QuickFix
{
    public abstract class AbstractInitiator : IInitiator
    {
        // from constructor
        private readonly IApplication _app;
        private readonly IMessageStoreFactory _storeFactory;
        private readonly SessionSettings _settings;
        private readonly ILogFactory _logFactory;
        private readonly IMessageFactory _msgFactory;

        private readonly object sync_ = new object();
        private readonly Dictionary<SessionID, Session> sessions_ = [];
        private volatile bool isStopped_ = true;
        private volatile Thread thread_;
        private SessionFactory sessionFactory_;

        #region Properties

        public bool IsStopped
        {
            get { return isStopped_; }
        }

        #endregion

        public AbstractInitiator(IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings)
            : this(app, storeFactory, settings, null, null)
        { }

        public AbstractInitiator(IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings, ILogFactory logFactory)
            : this(app, storeFactory, settings, logFactory, null)
        { }

        public AbstractInitiator(
            IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings, ILogFactory logFactory, IMessageFactory messageFactory)
        {
            _app = app;
            _storeFactory = storeFactory;
            _settings = settings;
            _logFactory = logFactory ?? new NullLogFactory();
            _msgFactory = messageFactory ?? new DefaultMessageFactory();

            HashSet<SessionID> definedSessions = _settings.GetSessions();
            if (0 == definedSessions.Count)
                throw new ConfigError("No sessions defined");
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // create all sessions
            sessionFactory_ = new SessionFactory(_app, _storeFactory, _logFactory, _msgFactory);
            foreach (SessionID sessionID in _settings.GetSessions())
            {
                Dictionary dict = _settings.Get(sessionID);
                CreateSession(sessionID, dict);
            }

            lock (sync_)
            {
                if (sessions_.Count == 0)
                    throw new ConfigError("No sessions defined for initiator");
            }

            // start it up
            isStopped_ = false;
            OnConfigure(_settings);
            thread_ = new Thread(new ThreadStart(OnStart));
            thread_.Start();
        }

        /// <summary>
        /// Add new session as an ad-hoc (dynamic) operation
        /// </summary>
        /// <param name="sessionID">ID of new session</param>
        /// <param name="dict">config settings for new session</param>
        /// <returns>true if session added successfully, false if session already exists or is not an initiator</returns>
        public bool AddSession(SessionID sessionID, Dictionary dict)
        {
            lock (_settings)
                if (!_settings.Has(sessionID)) // session won't be in settings if ad-hoc creation after startup
                    _settings.Set(sessionID, dict); // need to to this here to merge in default config settings
                else
                    return false; // session already exists

            if (CreateSession(sessionID, dict))
                return true;

            lock (_settings) // failed to create new session
                _settings.Remove(sessionID);

            return false;
        }

        /// <summary>
        /// Create session, either at start-up or as an ad-hoc operation
        /// </summary>
        /// <param name="sessionID">ID of new session</param>
        /// <param name="dict">config settings for new session</param>
        /// <returns>true if session added successfully, false if session already exists or is not an initiator</returns>
        private bool CreateSession(SessionID sessionID, Dictionary dict)
        {
            if (dict.GetString(SessionSettings.CONNECTION_TYPE) != "initiator")
                return false;

            lock (sync_)
            {
                if (sessions_.ContainsKey(sessionID))
                    return false;

                Session session = sessionFactory_.Create(sessionID, dict);
                session.ConnectionState = SessionConnectionState.Disconnected;
                sessions_[sessionID] = session;                
            }

            return true;
        }

        /// <summary>
        /// Ad-hoc removal of an existing session
        /// </summary>
        /// <param name="sessionID">ID of session to be removed</param>
        /// <param name="terminateActiveSession">if true, force disconnection and removal of session even if it has an active connection</param>
        /// <returns>true if session removed or not already present; false if could not be removed due to an active connection</returns>
        public bool RemoveSession(SessionID sessionID, bool terminateActiveSession)
        {
            Session session = null;
            bool disconnectRequired = false;
            lock (sync_)
            {
                if (sessions_.TryGetValue(sessionID, out session))
                {
                    if (session.IsLoggedOn && !terminateActiveSession)
                        return false;

                    if (session.ConnectionState == SessionConnectionState.Connected || session.ConnectionState == SessionConnectionState.Pending)
                        disconnectRequired = true;

                    session.ConnectionState = SessionConnectionState.None;
                    sessions_.Remove(sessionID);
                }
            }

            lock (_settings)
                _settings.Remove(sessionID);

            if (disconnectRequired)
                session.Disconnect("Dynamic session removal");

            OnRemove(sessionID); // ensure session's reader thread is gone before we dispose session
            session?.Dispose();
            return true;
        }

        /// <summary>
        /// Logout existing session and close connection.  Attempt graceful disconnect first.
        /// </summary>
        public void Stop()
        {
            Stop(false);
        }

        /// <summary>
        /// Logout existing session and close connection
        /// </summary>
        /// <param name="force">If true, terminate immediately.  </param>
        public void Stop(bool force)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Thread thread = thread_;
            if (IsStopped || thread is null)
                return;

            thread_ = null;
            lock (sync_)
            {
                // After this processing will be stopped in SocketInitiatorThread.SocketInitiatorThreadStart
                foreach (Session sess in sessions_.Values)
                {
                    if (sess.ConnectionState == SessionConnectionState.Connected || sess.ConnectionState == SessionConnectionState.Pending)
                        sess.Disable();
                }
            }

            if (!force)
            {
                // TODO change this duration to always exceed LogoutTimeout setting
                for (int second = 0; (second < 20) && IsLoggedOn; ++second)
                    Thread.Sleep(500);
            }

            OnStop();

            // Give OnStop() time to finish its business
            thread.Join(5000);

            // dispose all sessions and clear all session sets
            lock (sync_)
            {
                foreach (Session sess in sessions_.Values)
                    sess.Dispose();

                sessions_.Clear();
            }

            isStopped_ = true;
        }

        public bool IsLoggedOn
        {
            get
            {
                lock (sync_)
                {
                    foreach (Session sess in sessions_.Values)
                    {
                        if (sess.ConnectionState == SessionConnectionState.Connected && sess.IsLoggedOn)
                            return true;
                    }
                }

                return false;
            }
        }

        #region Virtual Methods

        /// <summary>
        /// Override this to configure additional implemenation-specific settings
        /// </summary>
        /// <param name="settings"></param>
        protected virtual void OnConfigure(SessionSettings settings)
        { }

        /// <summary>
        /// Implement this to provide custom reaction behavior to an ad-hoc session removal.
        /// (This is called after the session is removed.)
        /// </summary>
        /// <param name="sessionID">ID of session that was removed</param>
        protected virtual void OnRemove(SessionID sessionID)
        { }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Implemented to start connecting to targets.
        /// </summary>
        protected abstract void OnStart();
        /// <summary>
        /// Implemented to connect and poll for events.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected abstract bool OnPoll(double timeout);
        /// <summary>
        /// Implemented to stop a running initiator.
        /// </summary>
        protected abstract void OnStop();
        /// <summary>
        /// Implemented to connect a session to its target.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="settings"></param>
        protected abstract void DoConnect(Session session, QuickFix.Dictionary settings);

        #endregion

        #region Protected Methods

        protected void Connect()
        {
            lock (sync_)
            {
                foreach (Session session in sessions_.Values)
                {
                    if (session.ConnectionState == SessionConnectionState.Disconnected && session.IsEnabled)
                    {
                        if (session.IsNewSession)
                            session.Reset("New session");

                        if (session.IsSessionTime)
                            DoConnect(session, _settings.Get(session.SessionID));
                    }
                }
            }
        }

        protected void SetDisconnected(Session session)
        {
            if (session.ConnectionState == SessionConnectionState.None)
                return;

            lock (sync_)
            {
                if (!session.Disposed && sessions_.ContainsKey(session.SessionID))
                    session.ConnectionState = SessionConnectionState.Disconnected;
                else
                    session.ConnectionState = SessionConnectionState.None;
            }
        }

        #endregion


        /// <summary>
        /// Get the SessionIDs for the sessions managed by this initiator.
        /// </summary>
        /// <returns>the SessionIDs for the sessions managed by this initiator</returns>
        public HashSet<SessionID> GetSessionIDs()
        {
            lock (sync_)
                return [.. sessions_.Keys];
        }

        private bool _disposed;
        /// <summary>
        /// Any subclasses of AbstractInitiator should override this if they have resources to dispose
        /// that aren't already covered in its OnStop() handler.
        /// Any override should call base.Dispose(disposing).
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
                this.Stop();

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
