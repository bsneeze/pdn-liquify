using System;

namespace pyrochild.effects.common
{
    public class QueuedToolEventArgs : EventArgs
    {
        private readonly QueuedToolEventType eventtype;

        public QueuedToolEventArgs(QueuedToolEventType eventtype)
        {
            this.eventtype = eventtype;
        }

        public QueuedToolEventType EventType { get { return eventtype; } }
    }

    public class QueuedToolAbortEventArgs : QueuedToolEventArgs
    {
        public QueuedToolAbortEventArgs()
            : base(QueuedToolEventType.Abort)
        {
        }
    }
}
