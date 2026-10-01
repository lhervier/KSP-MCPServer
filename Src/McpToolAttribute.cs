using System;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Marks a method of another mod that this server offers as a tool. A mod references this assembly
    /// and puts the attribute on the methods it offers; it keeps working without this mod installed, since
    /// nothing reads the attribute then. The server finds the methods by the attribute's name, so a mod may
    /// also declare its own class of that name instead of referencing this assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class McpToolAttribute : Attribute
    {
        /// <summary>The tool's name, as the server publishes it.</summary>
        public string Name { get; }

        /// <summary>What the tool does, for whoever calls it.</summary>
        public string Description { get; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
