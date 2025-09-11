using System;
using System.Collections.Generic;

namespace QuickFix
{
    /// <summary>
    /// In-memory message store implementation
    /// </summary>
    public class MemoryStore : IMessageStore
    {
        #region Private Members

        Dictionary<SeqNumType, string> messages_;
        DateTime? creationTime;

        #endregion

        public MemoryStore()
        {
            messages_ = [];
            Reset();
        }

        public void Get(SeqNumType begSeqNo, SeqNumType endSeqNo, List<string> messages)
        {
            var msgs = messages_;
            if (msgs is null)
                return;

            for (SeqNumType current = begSeqNo; current <= endSeqNo; current++)
            {
                if (msgs.TryGetValue(current, out string value))
                    messages.Add(value);
            }
        }

        #region MessageStore Members

        public bool Set(SeqNumType msgSeqNum, string msg)
        {
            var msgs = messages_;
            if (msgs is null)
                return false;

            msgs[msgSeqNum] = msg;
            return true;
        }

        public SeqNumType NextSenderMsgSeqNum { get; set; }
        public SeqNumType NextTargetMsgSeqNum { get; set; }

        public void IncrNextSenderMsgSeqNum()
        { ++NextSenderMsgSeqNum; }

        public void IncrNextTargetMsgSeqNum()
        { ++NextTargetMsgSeqNum; }

        public DateTime? CreationTime
        {
            get { return creationTime; }
            internal set { creationTime = value; }
        }

        public void Reset()
        {
            NextSenderMsgSeqNum = 1;
            NextTargetMsgSeqNum = 1;
            messages_?.Clear();
            creationTime = DateTime.UtcNow;
        }

        public void Refresh()
        { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                messages_ = null;
        }
        #endregion
    }
}
