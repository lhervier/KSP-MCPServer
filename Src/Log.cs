using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Writes to KSP.log with the mod's prefix.</summary>
    internal static class Log
    {
        private const string Prefix = "[KSP-MCPServer] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }
    }
}
