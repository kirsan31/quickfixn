
namespace QuickFix
{
    /// <summary>
    /// FIXME - needs to log sessionIDs, timestamps, etc.
    /// </summary>
    public class ScreenLog : ILog
    {
        private readonly object sync_ = new object();
        private readonly bool logIncoming_;
        private readonly bool logOutgoing_;
        private readonly bool logEvent_;

        public ScreenLog(SessionID sessionID, bool logIncoming, bool logOutgoing, bool logEvent)
        {
            logIncoming_ = logIncoming;
            logOutgoing_ = logOutgoing;
            logEvent_    = logEvent;
        }

        #region ILog Members

        public void Clear()
        { }

        public void OnIncoming(string msg)
        {
            if (!logIncoming_)
                return;
            
            lock (sync_)
            {
                System.Console.WriteLine("<incoming> " + msg);
            }
        }

        public void OnOutgoing(string msg)
        {
            if (!logOutgoing_)
                return;

            lock (sync_)
            {
                System.Console.WriteLine("<outgoing> " + msg);
            }
        }

        public void OnEvent(string s)
        {
            if (!logEvent_)
                return;

            lock (sync_)
            {
                System.Console.WriteLine("<event> " + s);
            }
        }

        public void OnEvent(string s, bool error) => OnEvent(s);

        public void OnErrorEvent(string s) => OnEvent(s);
        #endregion
    }
}
