using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// Base class for queued tool renderers like Liquify and Smudge renderers.
    /// The UI can push its mouse events to the renderer, and the QTR will process them on another thread
    /// while keeping the UI responsive.
    /// Keeps track of the rect that has been changed so the caller can handle history and
    /// canvas invalidations.
    /// </summary>
    public abstract class QueuedToolRenderer : IDisposable 
    {
        Queue<QueuedToolEventArgs> eventQueue;

        WeakReference wr;

        Thread renderThread;

        protected ISurface Surface { get { return wr.Target as ISurface; } }

        object invalidrectlock = new object();
        private Rectangle totalinvalidrect = Rectangle.Empty;

        /// <summary>
        /// QTR keeps track of the invalid rect, or the area of the image that has been changed by the renderer.
        /// This rect is used by the caller for history or canvas invalidation purposes.
        /// </summary>
        /// <returns>The Rectangle encompassing any changes that have been made by the QTR on the
        /// Surface since the last time PopTotalInvalidRect() was called</returns>
        public Rectangle PopTotalInvalidRect()
        {
            lock (invalidrectlock)
            {
                Rectangle retval = totalinvalidrect;
                totalinvalidrect = Rectangle.Empty;
                return retval;
            }
        }

        private void PushTotalInvalidRect(Rectangle newrect)
        {
            lock (invalidrectlock)
            {
                totalinvalidrect = Rectangle.Union(totalinvalidrect, newrect);
            }
        }

        public QueuedToolRenderer(ISurface surface)
        {
            wr = new WeakReference(surface);

            eventQueue = new Queue<QueuedToolEventArgs>();
        }

        public void AddEvent(QueuedToolEventArgs args)
        {
            if (disposed)
            {
                return;
            }

            lock (eventQueue)
            {
                eventQueue.Enqueue(args);
                OnEventQueued();
            }
        }

        public void AddEvents(IEnumerable<QueuedToolEventArgs> args)
        {
            if (disposed)
            {
                return;
            }

            lock (eventQueue)
            {
                foreach(QueuedToolEventArgs arg in args)
                eventQueue.Enqueue(arg);
                OnEventQueued();
            }
        }

        // Called with the queue locked. One render thread serves the renderer's whole life,
        // sleeping on the queue between events.
        private void OnEventQueued()
        {
            if (renderThread == null)
            {
                renderThread = new Thread(new ThreadStart(Render));

                // so a hung render can't keep the program from exiting
                renderThread.IsBackground = true;
                renderThread.Start();
            }

            Monitor.Pulse(eventQueue);
        }

        // set under the queue lock: the render thread exits once the queue is empty
        bool stopping = false;

        volatile bool aborted = false;
        public void Abort()
        {
            aborted = true;
            lock (eventQueue)
            {
                eventQueue.Clear();
                eventQueue.Enqueue(new QueuedToolAbortEventArgs());
                OnEventQueued();
            }
        }

        public bool IsAborted { get { return aborted; } }

        public int GetQueueSize()
        {
            lock (eventQueue)
            {
                return eventQueue.Count;
            }
        }

        private void Render()
        {
            bool didsomething = false;
            while (true)
            {
                QueuedToolEventArgs args;
                bool emptied = false;
                lock (eventQueue)
                {
                    while (eventQueue.Count == 0 && !didsomething && !stopping)
                    {
                        Monitor.Wait(eventQueue);
                    }

                    if (eventQueue.Count == 0)
                    {
                        if (!didsomething)
                        {
                            return; // stopping
                        }
                        emptied = true;
                        args = null;
                    }
                    else
                    {
                        args = eventQueue.Dequeue();
                    }

                    // when the queue is backed up, skip mouse moves that a later one makes redundant
                    while (args != null
                        && args.EventType == QueuedToolEventType.MouseMove
                        && eventQueue.Count > 0
                        && eventQueue.Peek() != null
                        && eventQueue.Peek().EventType == QueuedToolEventType.MouseMove
                        && CanCoalesce(args, eventQueue.Peek()))
                    {
                        args = eventQueue.Dequeue();
                    }
                }

                try
                {
                    if (emptied)
                    {
                        didsomething = false;
                        OnQueueEmptied();
                        continue;
                    }

                    didsomething = true;
                    if (args != null)
                    {
                        Process(args);
                    }
                }
                catch (Exception ex) when (Error != null)
                {
                    // An exception leaving this thread would take the host down. Carry on with the
                    // next event: the mouse-up that ends a stroke must still get through.
                    Error(this, new ThreadExceptionEventArgs(ex));
                }
            }
        }

        /// <summary>
        /// Raised on the render thread when handling an event throws. With no subscribers the
        /// exception is left unhandled.
        /// </summary>
        public event ThreadExceptionEventHandler Error;

        private void Process(QueuedToolEventArgs args)
        {
            switch (args.EventType)
            {
                case QueuedToolEventType.MouseDown:
                    QueuedMouseDown(args);
                    break;
                case QueuedToolEventType.MouseMove:
                    QueuedMouseMove(args);
                    break;
                case QueuedToolEventType.MouseUp:
                    QueuedMouseUp(args);
                    break;
                case QueuedToolEventType.Abort:
                    QueuedAbort();
                    break;
                case QueuedToolEventType.MouseHold:
                    QueuedMouseHold(args);
                    break;
                case QueuedToolEventType.Custom:
                    QueuedCustomEvent(args);
                    break;
                default:
                    throw new ArgumentException("invalid event in queue");
            }
        }

        public event EventHandler CustomEvent;
        private void QueuedCustomEvent(QueuedToolEventArgs args)
        {
            OnCustomEvent(args);
            if (CustomEvent != null)
            {
                CustomEvent(this, args);
            }
        }

        public event QueuedToolEventHandler MouseHold;
        private void QueuedMouseHold(QueuedToolEventArgs args)
        {
            OnMouseHold(args);
            if (MouseHold != null)
            {
                MouseHold(this, args);
            }
        }

        public event QueuedToolEventHandler MouseUp;
        private void QueuedMouseUp(QueuedToolEventArgs args)
        {
            OnMouseUp(args);
            if (MouseUp != null)
            {
                MouseUp(this, args);
            }
        }

        public event QueuedToolEventHandler MouseMove;
        private void QueuedMouseMove(QueuedToolEventArgs args)
        {
            OnMouseMove(args);
            if (MouseMove != null)
            {
                MouseMove(this, args);
            }
        }

        public event QueuedToolEventHandler MouseDown;
        private void QueuedMouseDown(QueuedToolEventArgs args)
        {
            OnMouseDown(args);
            if (MouseDown != null)
            {
                MouseDown(this, args);
            }
        }

        protected virtual void OnCustomEvent(QueuedToolEventArgs args)
        {
        }

        protected virtual void OnMouseHold(QueuedToolEventArgs args)
        {
        }

        protected virtual void OnMouseUp(QueuedToolEventArgs args)
        {
        }

        protected virtual void OnMouseMove(QueuedToolEventArgs args)
        {
        }

        protected virtual void OnMouseDown(QueuedToolEventArgs args)
        {
        }

        /// <summary>
        /// Called on the render thread when two MouseMove events are next to each other in the queue.
        /// Return true to drop the earlier one and go straight to the later one.
        /// </summary>
        protected virtual bool CanCoalesce(QueuedToolEventArgs earlier, QueuedToolEventArgs later)
        {
            return false;
        }

        private void QueuedAbort()
        {
            lock (eventQueue)
            {
                eventQueue.Clear();
            }
            OnAborted();
            aborted = false;
        }

        public event EventHandler Aborted;
        private void OnAborted()
        {
            if (Aborted != null)
            {
                Aborted(this, EventArgs.Empty);
                aborted = false;
            }
        }

        public event InvalidateEventHandler Invalidated;
        protected void OnInvalidated(Rectangle invalidRect)
        {
            invalidRect.Intersect(Surface.Bounds);
            PushTotalInvalidRect(invalidRect);
            if (Invalidated != null)
            {
                Invalidated(this, new InvalidateEventArgs(invalidRect));
            }
        }

        public event EventHandler QueueEmptied;
        private void OnQueueEmptied()
        {
            if (QueueEmptied != null)
                QueueEmptied(this, EventArgs.Empty);
        }

        private bool disposed = false;
        public bool Disposed { get { return disposed; } }

        /// <summary>
        /// Aborts whatever is queued and waits for the render thread to finish, so after this returns
        /// no more events are raised. Event handlers must not block on the disposing thread.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Abort();
            disposed = true;

            Thread thread;
            lock (eventQueue)
            {
                stopping = true;
                Monitor.PulseAll(eventQueue);
                thread = renderThread;
            }

            if (thread != null && thread != Thread.CurrentThread)
            {
                thread.Join();
            }

            OnDispose();
        }

        protected virtual void OnDispose()
        {
        }
    }
}