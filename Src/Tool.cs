using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

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

        public Tool(string name, string description, Dictionary<string, object> inputSchema, Func<ToolCall, IEnumerator> run)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Run = run;
        }
    }

    /// <summary>
    /// A call to a tool: its arguments, and the result the tool's coroutine fills before it ends. A tool
    /// that never sets a result answers with an empty text.
    /// </summary>
    internal sealed class ToolCall
    {
        public readonly Dictionary<string, object> Arguments;

        /// <summary>The MCP content items of the answer: text or image.</summary>
        public readonly List<object> Content = new List<object>();

        /// <summary>Whether the tool failed: the MCP answer carries <c>isError</c>.</summary>
        public bool IsError;

        public ToolCall(Dictionary<string, object> arguments)
        {
            Arguments = arguments ?? new Dictionary<string, object>();
        }

        /// <summary>Adds a text item; a non-string value is written as JSON.</summary>
        public void Text(object value)
        {
            Content.Add(new Dictionary<string, object>
            {
                { "type", "text" },
                { "text", value as string ?? Json.Write(value) }
            });
        }

        /// <summary>Adds a PNG image item.</summary>
        public void Png(byte[] png)
        {
            Content.Add(new Dictionary<string, object>
            {
                { "type", "image" },
                { "data", Convert.ToBase64String(png) },
                { "mimeType", "image/png" }
            });
        }

        /// <summary>Marks the call as failed, with a message.</summary>
        public void Fail(string message)
        {
            IsError = true;
            Text(message);
        }

        public bool Has(string name)
        {
            return Arguments.ContainsKey(name) && Arguments[name] != null;
        }

        public string String(string name, string fallback = null)
        {
            return Has(name) ? Convert.ToString(Arguments[name], CultureInfo.InvariantCulture) : fallback;
        }

        public double Number(string name, double fallback = double.NaN)
        {
            return Has(name) ? Convert.ToDouble(Arguments[name], CultureInfo.InvariantCulture) : fallback;
        }

        public bool Bool(string name, bool fallback = false)
        {
            return Has(name) ? Convert.ToBoolean(Arguments[name], CultureInfo.InvariantCulture) : fallback;
        }
    }

    /// <summary>Builds the JSON schemas of tool arguments.</summary>
    internal static class Schema
    {
        /// <summary>An object schema from (name, type, description, required) entries.</summary>
        public static Dictionary<string, object> Object(params Prop[] props)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            List<object> required = new List<object>();
            foreach (Prop p in props)
            {
                properties[p.Name] = new Dictionary<string, object> { { "type", p.Type }, { "description", p.Description } };
                if (p.Required)
                {
                    required.Add(p.Name);
                }
            }
            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties }
            };
            if (required.Count > 0)
            {
                schema["required"] = required;
            }
            return schema;
        }

        public static Prop P(string name, string type, string description, bool required = false)
        {
            return new Prop { Name = name, Type = type, Description = description, Required = required };
        }

        internal struct Prop
        {
            public string Name;
            public string Type;
            public string Description;
            public bool Required;
        }
    }
}
