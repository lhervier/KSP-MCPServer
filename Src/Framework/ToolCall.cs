using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace com.github.lhervier.ksp.mcpserver
{
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

        /// <summary>
        /// The value an argument names among <paramref name="choices"/>, its keys compared without case;
        /// the one <paramref name="fallback"/> names when the argument is left out. Returns false after
        /// failing the call with the keys it accepts when the argument names none of them.
        /// </summary>
        public bool Choice<T>(string name, string fallback, IDictionary<string, T> choices, out T value)
        {
            string given = String(name, fallback) ?? "";
            foreach (KeyValuePair<string, T> choice in choices)
            {
                if (string.Equals(choice.Key, given, StringComparison.OrdinalIgnoreCase))
                {
                    value = choice.Value;
                    return true;
                }
            }
            string[] keys = choices.Keys.ToArray();
            Fail(name + ": " + (keys.Length > 1
                ? string.Join(", ", keys, 0, keys.Length - 1) + " or " + keys[keys.Length - 1]
                : string.Join("", keys)));
            value = default(T);
            return false;
        }
    }
}
