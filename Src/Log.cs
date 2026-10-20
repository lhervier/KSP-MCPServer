using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Writes to KSP.log with the mod's prefix. Callable from any thread: a message logged off Unity's main
    /// thread is written from the main thread, at its next <see cref="Flush"/>.
    /// </summary>
    internal static class Log
    {
        private const string Prefix = "[KSP-MCPServer] ";

        // The messages logged off the main thread, waiting for Flush: true for an error.
        private static readonly Queue<KeyValuePair<bool, string>> Deferred = new Queue<KeyValuePair<bool, string>>();
        private static int _mainThreadId = -1;

        /// <summary>Marks the calling thread as Unity's main thread. Called once, from the main thread.</summary>
        public static void SetMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static void Info(string message)
        {
            Write(false, message);
        }

        public static void Error(string message)
        {
            Write(true, message);
        }

        /// <summary>Writes the messages logged off the main thread since the last call. Called on the main thread.</summary>
        public static void Flush()
        {
            while (true)
            {
                KeyValuePair<bool, string> deferred;
                lock (Deferred)
                {
                    if (Deferred.Count == 0)
                    {
                        return;
                    }
                    deferred = Deferred.Dequeue();
                }
                Emit(deferred.Key, deferred.Value);
            }
        }

        private static void Write(bool error, string message)
        {
            // KSP handles a log message on the thread that logs it (Application.logMessageReceivedThreaded), and
            // its debug console may build a TextMeshPro object there: off the main thread, that crashes the game.
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                lock (Deferred)
                {
                    Deferred.Enqueue(new KeyValuePair<bool, string>(error, message));
                }
                return;
            }
            Emit(error, message);
        }

        private static void Emit(bool error, string message)
        {
            if (error)
            {
                Debug.LogError(Prefix + message);
            }
            else
            {
                Debug.Log(Prefix + message);
            }
        }
    }
}
