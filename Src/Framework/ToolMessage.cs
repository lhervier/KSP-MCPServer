using System;
using System.Collections;
using System.Collections.Generic;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// The short message at the top of the screen that names the tool being run, so that whoever watches the
    /// game sees what drives it. One message at a time, replaced by the next tool's; never on a screenshot.
    /// </summary>
    internal static class ToolMessage
    {
        private const string ScreenshotTool = "screenshot";
        private const int MaxArgumentsLength = 80;
        private const float Duration = 3f;

        private static bool _enabled = true;
        private static ScreenMessage _shown;
        private static bool _failed;

        /// <summary>Turns the messages on or off, from the settings read when KSP starts.</summary>
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        /// <summary>
        /// Wraps the coroutine of a tool so that, on the main thread, the message naming the tool is posted
        /// before the tool runs.
        /// </summary>
        public static IEnumerator Announced(string name, Dictionary<string, object> arguments, IEnumerator run)
        {
            Announce(name, arguments);
            yield return run;
        }

        /// <summary>Takes the message off the screen, if one is shown. Never throws.</summary>
        public static void Remove()
        {
            if (_shown == null)
            {
                return;
            }
            try
            {
                ScreenMessages.RemoveMessage(_shown);
            }
            catch (Exception e)
            {
                Failed(e);
            }
            _shown = null;
        }

        // Replaces the message by one naming this tool. The screenshot tool gets none, and takes the last one
        // off the screen: what it captures must not show it. Nothing while the game loads, before it has any
        // interface to show a message in.
        private static void Announce(string name, Dictionary<string, object> arguments)
        {
            Remove();
            if (!_enabled || _failed || name == ScreenshotTool)
            {
                return;
            }
            try
            {
                if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER
                    || ScreenMessages.Instance == null)
                {
                    return;
                }
                _shown = ScreenMessages.PostScreenMessage("MCP: " + name + Abridged(arguments), Duration,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception e)
            {
                Failed(e);
            }
        }

        // The arguments as JSON, cut short when long; nothing when there are none.
        private static string Abridged(Dictionary<string, object> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return "";
            }
            string json = Json.Write(arguments);
            return " " + (json.Length > MaxArgumentsLength ? json.Substring(0, MaxArgumentsLength) + "..." : json);
        }

        // A game where the messages fail gets no more of them, and one line of log only.
        private static void Failed(Exception e)
        {
            _failed = true;
            _shown = null;
            Log.Error("Screen messages turned off: " + e.Message);
        }
    }
}
