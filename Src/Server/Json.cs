using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// A minimal JSON reader and writer. Objects are read as <c>Dictionary&lt;string, object&gt;</c>, arrays
    /// as <c>List&lt;object&gt;</c>, numbers as <c>double</c>, and literals as <c>bool</c> or <c>null</c>.
    /// </summary>
    internal static class Json
    {
        /// <summary>Parses a JSON text. Throws <see cref="FormatException"/> when it is not valid JSON.</summary>
        public static object Parse(string text)
        {
            int index = 0;
            object value = ReadValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
            {
                throw new FormatException("Unexpected text after the JSON value at " + index);
            }
            return value;
        }

        /// <summary>Writes a value made of dictionaries, lists, strings, numbers, booleans and nulls.</summary>
        public static string Write(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
            {
                throw new FormatException("Unexpected end of JSON");
            }
            char c = s[i];
            switch (c)
            {
                case '{':
                    return ReadObject(s, ref i);
                case '[':
                    return ReadArray(s, ref i);
                case '"':
                    return ReadString(s, ref i);
                case 't':
                    Expect(s, ref i, "true");
                    return true;
                case 'f':
                    Expect(s, ref i, "false");
                    return false;
                case 'n':
                    Expect(s, ref i, "null");
                    return null;
                default:
                    return ReadNumber(s, ref i);
            }
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Expected '" + literal + "' at " + i);
            }
            i += literal.Length;
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ReadString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException("Expected ':' at " + i);
                }
                i++;
                result[key] = ReadValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return result;
                }
                throw new FormatException("Expected ',' or '}' at " + i);
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            List<object> result = new List<object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return result;
            }
            while (true)
            {
                result.Add(ReadValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return result;
                }
                throw new FormatException("Expected ',' or ']' at " + i);
            }
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
            {
                throw new FormatException("Expected a string at " + i);
            }
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length)
                {
                    break;
                }
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                        {
                            throw new FormatException("Truncated \\u escape at " + i);
                        }
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("Unknown escape '\\" + e + "' at " + i);
                }
            }
            throw new FormatException("Unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            {
                i++;
            }
            if (start == i)
            {
                throw new FormatException("Unexpected character '" + s[i] + "' at " + i);
            }
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string str)
            {
                WriteString(sb, str);
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in dict)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    WriteString(sb, pair.Key);
                    sb.Append(':');
                    WriteValue(sb, pair.Value);
                }
                sb.Append('}');
            }
            else if (value is IEnumerable list)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
            }
            else if (value is double d)
            {
                // JSON has no NaN nor infinities.
                sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is float f)
            {
                sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is int || value is long || value is short || value is byte || value is uint || value is ulong)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else
            {
                WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
