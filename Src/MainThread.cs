using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Runs coroutines handed over by the HTTP thread on Unity's main thread, where everything in KSP has to
    /// be touched, and lets the HTTP thread wait for them to end.
    /// </summary>
    internal static class MainThread
    {
        /// <summary>How long a tool may run before its caller stops waiting.</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        private sealed class Job
        {
            public IEnumerator Routine;
            public Exception Failure;
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
        }

        private static readonly Queue<Job> Pending = new Queue<Job>();
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
        /// Queues a coroutine for the main thread and blocks until it ends. Returns the exception it threw,
        /// or null. Called from the HTTP thread only.
        /// </summary>
        public static Exception RunAndWait(IEnumerator routine)
        {
            Job job = new Job { Routine = routine };
            lock (Pending)
            {
                Pending.Enqueue(job);
            }
            if (!job.Done.WaitOne(Timeout))
            {
                return new TimeoutException("the tool did not finish within " + Timeout.TotalMinutes + " minutes");
            }
            return job.Failure;
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
        // handed back instead of only reaching the log. Nested coroutines yielded by the job are stepped the
        // same way.
        private static IEnumerator Guard(Job job)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(job.Routine);
            while (stack.Count > 0)
            {
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
            job.Done.Set();
        }
    }
}
