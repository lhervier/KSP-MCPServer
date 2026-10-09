using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Runs coroutines handed over by the HTTP threads on Unity's main thread, where everything in KSP has to
    /// be touched, and lets each HTTP thread wait for its own to end. Several may run at once; those that are
    /// not concurrent run one at a time, and the second one is refused rather than kept waiting. A running
    /// coroutine can be cancelled: it is stopped between two of its steps, and its <c>finally</c> blocks run.
    /// </summary>
    internal static class MainThread
    {
        /// <summary>How long a tool may run before its caller stops waiting and the tool is cancelled.</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        /// <summary>A coroutine to run, and how it ended.</summary>
        public sealed class Job
        {
            public readonly string Name;
            public readonly object RequestId;
            public readonly bool Concurrent;
            public readonly DateTime Started = DateTime.UtcNow;
            internal readonly IEnumerator Routine;
            internal readonly ManualResetEvent Done = new ManualResetEvent(false);
            internal Exception Failure;
            private volatile string _cancelReason;

            internal Job(string name, object requestId, bool concurrent, IEnumerator routine)
            {
                Name = name;
                RequestId = requestId;
                Concurrent = concurrent;
                Routine = routine;
            }

            /// <summary>Why the job is to be cancelled, or null.</summary>
            internal string CancelReason
            {
                get { return _cancelReason; }
            }

            /// <summary>Whether the job has ended, however it ended.</summary>
            public bool IsDone
            {
                get { return Done.WaitOne(0); }
            }

            internal void Cancel(string reason)
            {
                if (_cancelReason == null)
                {
                    _cancelReason = reason;
                }
            }
        }

        /// <summary>Thrown back to the caller of a job that was cancelled.</summary>
        public sealed class CancelledException : Exception
        {
            public CancelledException(string reason) : base(reason)
            {
            }
        }

        /// <summary>Thrown back to the caller of a job refused because another one runs.</summary>
        public sealed class BusyException : Exception
        {
            public BusyException(string message) : base(message)
            {
            }
        }

        private static readonly Queue<Job> Pending = new Queue<Job>();
        private static readonly List<Job> Running = new List<Job>();
        private static MonoBehaviour _host;

        /// <summary>The object whose coroutines run the jobs, which lives across scene changes.</summary>
        public static MonoBehaviour Host
        {
            get { return _host; }
        }

        /// <summary>Sets the object whose coroutines run the jobs: one that lives across scene changes.</summary>
        public static void SetHost(MonoBehaviour host)
        {
            _host = host;
        }

        /// <summary>
        /// Queues a coroutine for the main thread and blocks until it ends. Returns null when it ended on its
        /// own; the exception it threw; a <see cref="CancelledException"/> when it was cancelled; a
        /// <see cref="BusyException"/>, without running it, when it is not concurrent and another such job
        /// is queued or running; a <see cref="TimeoutException"/> after <see cref="Timeout"/>, the job being
        /// cancelled then. Called from an HTTP thread only.
        /// </summary>
        public static Exception RunAndWait(string name, object requestId, bool concurrent, IEnumerator routine)
        {
            Job job = new Job(name, requestId, concurrent, routine);
            lock (Pending)
            {
                if (!concurrent)
                {
                    Job other = Running.Find(j => !j.Concurrent);
                    if (other != null)
                    {
                        return new BusyException(other.Name + " is running (for " +
                            (int)(DateTime.UtcNow - other.Started).TotalSeconds + " s): wait for it to end, or stop it");
                    }
                }
                Pending.Enqueue(job);
                Running.Add(job);
            }
            if (!job.Done.WaitOne(Timeout))
            {
                job.Cancel("the tool did not finish within " + Timeout.TotalMinutes + " minutes");
                return new TimeoutException(job.CancelReason);
            }
            return job.Failure;
        }

        /// <summary>
        /// Asks the queued or running jobs that <paramref name="match"/> picks to stop, for
        /// <paramref name="reason"/>; returns them. They stop at their next step, on the main thread. Called
        /// from any thread.
        /// </summary>
        public static List<Job> Cancel(Predicate<Job> match, string reason)
        {
            List<Job> cancelled;
            lock (Pending)
            {
                cancelled = Running.FindAll(match);
            }
            foreach (Job job in cancelled)
            {
                job.Cancel(reason);
            }
            return cancelled;
        }

        /// <summary>Starts the queued jobs. Called by the host every frame.</summary>
        public static void Pump()
        {
            while (true)
            {
                Job job;
                lock (Pending)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }
                    job = Pending.Dequeue();
                }
                _host.StartCoroutine(Guard(job));
            }
        }

        // Steps the job's coroutine by hand, so that an exception thrown in any of its steps is caught and
        // handed back instead of only reaching the log, and so that it can be cancelled between two steps.
        // Nested coroutines yielded by the job are stepped the same way.
        private static IEnumerator Guard(Job job)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(job.Routine);
            while (stack.Count > 0)
            {
                if (job.CancelReason != null)
                {
                    job.Failure = new CancelledException(job.CancelReason);
                    break;
                }
                IEnumerator top = stack.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception e)
                {
                    job.Failure = e;
                    break;
                }
                if (!more)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return top.Current;
            }

            // A coroutine left unfinished, cancelled or broken, is disposed of, innermost first: that runs its
            // finally blocks, which give back what it holds (the wheels, an event handler).
            while (stack.Count > 0)
            {
                IDisposable disposable = stack.Pop() as IDisposable;
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception e)
                {
                    Log.Error(job.Name + " threw while it was stopped: " + e);
                }
            }
            lock (Pending)
            {
                Running.Remove(job);
            }
            job.Done.Set();
        }
    }
}
