using System;
using System.Collections;
using System.Collections.Generic;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// One tool offered to the MCP client: its name, description, JSON schema of its arguments, and the
    /// coroutine that runs it on Unity's main thread.
    /// </summary>
    internal sealed class Tool
    {
        public readonly string Name;
        public readonly string Description;
        public readonly Dictionary<string, object> InputSchema;
        public readonly Func<ToolCall, IEnumerator> Run;

        /// <summary>
        /// Whether the tool may run while another one runs: true for a tool that only reads the game, or
        /// whose action cannot get in the way of another tool's (the camera, the pause, stopping a drive).
        /// Two tools that are not are never run together: the second fails at once.
        /// </summary>
        public bool Concurrent { get; set; }

        public Tool(string name, string description, Dictionary<string, object> inputSchema, Func<ToolCall, IEnumerator> run)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Run = run;
        }
    }
}
