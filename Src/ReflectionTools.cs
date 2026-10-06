using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that reach into other mods by reflection: read or write a field or property of a loaded
    /// MonoBehaviour (or a static one), and call a method; a window's position, for one, is a field.
    /// </summary>
    internal static class ReflectionTools
    {
        private const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        private const string MatchDescription =
            "path=value: take the first loaded object whose path (as in get_member) reads this value, instead of " +
            "the first loaded object";

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("get_member",
                "Reads a field or property of the first loaded object of a type (its full or short name; the full " +
                "name for a type of Unity), or a static one, or follows a path from it: names of fields and " +
                "properties, indexes in a list, and [Type] for the components of that type on a Unity object and " +
                "its children, separated by dots (objects.0.[UnityEngine.Renderer].0.bounds). Numbers, strings, " +
                "booleans, vectors, rotations, bounds and rects come back as JSON; lists as their items.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("member", "string", "field or property name, or a path", true),
                    Schema.P("match", "string", MatchDescription)),
                GetMember);
            yield return new Tool("set_member",
                "Writes a field or property of the first loaded object of a type, or a static one. The value is a " +
                "number, string or boolean, or an object {x, y, width, height} for a Rect, {x, y, z} for a vector.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("member", "string", "field or property name", true),
                    Schema.P("value", "object", "the new value", true),
                    Schema.P("match", "string", MatchDescription)),
                SetMember);
            yield return new Tool("call_method",
                "Calls a method without arguments of the first loaded object of a type, or a static one, and " +
                "returns what it returns.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("method", "string", "method name", true),
                    Schema.P("match", "string", MatchDescription)),
                CallMethod);
        }

        // ----- generic reflection -----

        private static Type FindType(string name)
        {
            foreach (AssemblyLoader.LoadedAssembly loaded in AssemblyLoader.loadedAssemblies)
            {
                Type[] types;
                try
                {
                    types = loaded.assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                Type match = types.FirstOrDefault(t => t.FullName == name) ?? types.FirstOrDefault(t => t.Name == name);
                if (match != null)
                {
                    return match;
                }
            }
            Type game = typeof(Vessel).Assembly.GetType(name) ?? typeof(Vessel).Assembly.GetTypes().FirstOrDefault(t => t.Name == name);
            if (game != null)
            {
                return game;
            }
            // Unity's own types (UnityEngine.Renderer) are in none of the assemblies above: by full name only.
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
        }

        /// <summary>Splits a path at its dots, except the dots inside a [Type] step.</summary>
        private static List<string> SplitPath(string path)
        {
            List<string> steps = new List<string>();
            int start = 0;
            int brackets = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '[')
                {
                    brackets++;
                }
                else if (path[i] == ']')
                {
                    brackets--;
                }
                else if (path[i] == '.' && brackets == 0)
                {
                    steps.Add(path.Substring(start, i - start));
                    start = i + 1;
                }
            }
            steps.Add(path.Substring(start));
            return steps;
        }

        /// <summary>The field or property of a type with this name, static or not, public or not; null if none.</summary>
        private static MemberInfo FindMember(Type type, string name)
        {
            return type.GetMember(name, AnyMember).FirstOrDefault(m => m is FieldInfo || m is PropertyInfo);
        }

        /// <summary>
        /// Follows a path from an object of a type (null for a path that starts with a static member) and gives the
        /// value it ends on, or false and why it could not.
        /// </summary>
        private static bool ReadPath(Type type, object target, List<string> path, out object value, out string error)
        {
            value = target;
            error = null;
            Type current = type;
            for (int i = 0; i < path.Count; i++)
            {
                string step = path[i];
                // Only the first step may start from nothing: a static member.
                if (value == null && i > 0)
                {
                    error = "null before " + step;
                    return false;
                }
                if (step.StartsWith("[") && step.EndsWith("]"))
                {
                    // [Type]: the components of that type on a GameObject or a component's GameObject, children included.
                    Type componentType = FindType(step.Substring(1, step.Length - 2));
                    GameObject gameObject = value is GameObject g ? g : (value as Component)?.gameObject;
                    if (componentType == null || gameObject == null)
                    {
                        error = componentType == null ? "Unknown type " + step : step + " needs a Unity object, not " + current.Name;
                        return false;
                    }
                    value = gameObject.GetComponentsInChildren(componentType, true);
                }
                else if (int.TryParse(step, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    if (!(value is IEnumerable items))
                    {
                        error = step + " is an index, and " + current.Name + " is not a list";
                        return false;
                    }
                    List<object> list = items.Cast<object>().ToList();
                    if (index < 0 || index >= list.Count)
                    {
                        error = "index " + step + " out of a list of " + list.Count;
                        return false;
                    }
                    value = list[index];
                }
                else
                {
                    MemberInfo member = FindMember(current, step);
                    if (member == null)
                    {
                        error = "No member " + step + " in " + current.FullName;
                        return false;
                    }
                    value = member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value, null);
                }
                current = value?.GetType() ?? typeof(object);
            }
            return true;
        }

        // The object a member is read on: null for a static member, the first loaded instance otherwise.
        private static bool Target(ToolCall call, Type type, MemberInfo member, out object target)
        {
            target = null;
            bool isStatic = member is FieldInfo f ? f.IsStatic
                : member is PropertyInfo p ? (p.GetGetMethod(true) ?? p.GetSetMethod(true)).IsStatic
                : member is MethodInfo m && m.IsStatic;
            if (isStatic)
            {
                return true;
            }
            if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                call.Fail(type.FullName + "." + member.Name + " is not static, and " + type.Name + " is not a Unity object to look for");
                return false;
            }
            if (!call.Has("match"))
            {
                target = UnityEngine.Object.FindObjectOfType(type);
                if (target == null)
                {
                    call.Fail("No loaded " + type.FullName);
                    return false;
                }
                return true;
            }
            string match = call.String("match");
            int equals = match.IndexOf('=');
            if (equals <= 0)
            {
                call.Fail("match is path=value, not " + match);
                return false;
            }
            List<string> path = SplitPath(match.Substring(0, equals));
            string expected = match.Substring(equals + 1);
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(type))
            {
                if (ReadPath(type, candidate, path, out object read, out string _)
                    && Convert.ToString(read, CultureInfo.InvariantCulture) == expected)
                {
                    target = candidate;
                    return true;
                }
            }
            call.Fail("No loaded " + type.FullName + " whose " + match);
            return false;
        }

        private static IEnumerator GetMember(ToolCall call)
        {
            Type type = FindType(call.String("type"));
            if (type == null)
            {
                call.Fail("Unknown type " + call.String("type"));
                yield break;
            }
            List<string> path = SplitPath(call.String("member"));
            // The first step tells whether the path starts on a static member or on a loaded object.
            MemberInfo member = FindMember(type, path[0]);
            object target;
            if (member == null)
            {
                call.Fail("No member " + path[0] + " in " + type.FullName);
                yield break;
            }
            if (!Target(call, type, member, out target))
            {
                yield break;
            }
            if (!ReadPath(type, target, path, out object value, out string error))
            {
                call.Fail(error);
                yield break;
            }
            call.Text(new Dictionary<string, object> { { "value", ToJson(value, 2) } });
        }

        private static IEnumerator SetMember(ToolCall call)
        {
            Type type = FindType(call.String("type"));
            if (type == null)
            {
                call.Fail("Unknown type " + call.String("type"));
                yield break;
            }
            MemberInfo member = type.GetMember(call.String("member"), AnyMember).FirstOrDefault();
            object target;
            if (member == null)
            {
                call.Fail("No member " + call.String("member") + " in " + type.FullName);
                yield break;
            }
            if (!Target(call, type, member, out target))
            {
                yield break;
            }
            Type valueType = member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;
            object value = FromJson(call.Arguments["value"], valueType);
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
            }
            else
            {
                ((PropertyInfo)member).SetValue(target, value, null);
            }
            call.Text(new Dictionary<string, object> { { "value", ToJson(value, 2) } });
        }

        private static IEnumerator CallMethod(ToolCall call)
        {
            Type type = FindType(call.String("type"));
            if (type == null)
            {
                call.Fail("Unknown type " + call.String("type"));
                yield break;
            }
            MethodInfo method = type.GetMethods(AnyMember)
                .FirstOrDefault(m => m.Name == call.String("method") && m.GetParameters().Length == 0);
            object target;
            if (method == null)
            {
                call.Fail("No method " + call.String("method") + "() in " + type.FullName);
                yield break;
            }
            if (!Target(call, type, method, out target))
            {
                yield break;
            }
            object result = method.Invoke(target, null);
            call.Text(new Dictionary<string, object> { { "returned", ToJson(result, 2) } });
        }

        /// <summary>A value as JSON-ready data; objects of other types show their public fields, to a depth.</summary>
        public static object ToJson(object value, int depth)
        {
            if (value == null || value is string || value is bool || value is double || value is float
                || value is int || value is long || value is short || value is byte || value is uint)
            {
                return value;
            }
            if (value is Enum)
            {
                return value.ToString();
            }
            if (value is Vector3 v3)
            {
                return new Dictionary<string, object> { { "x", (double)v3.x }, { "y", (double)v3.y }, { "z", (double)v3.z } };
            }
            if (value is Vector3d vd)
            {
                return new Dictionary<string, object> { { "x", vd.x }, { "y", vd.y }, { "z", vd.z } };
            }
            if (value is Quaternion q)
            {
                return new Dictionary<string, object> { { "x", (double)q.x }, { "y", (double)q.y }, { "z", (double)q.z }, { "w", (double)q.w } };
            }
            if (value is Bounds b)
            {
                return new Dictionary<string, object> { { "center", ToJson(b.center, depth) }, { "size", ToJson(b.size, depth) } };
            }
            if (value is Rect r)
            {
                return new Dictionary<string, object>
                {
                    { "x", (double)r.x }, { "y", (double)r.y }, { "width", (double)r.width }, { "height", (double)r.height }
                };
            }
            if (value is UnityEngine.Object unity)
            {
                return unity.ToString();
            }
            if (value is IDictionary dictionary)
            {
                if (depth <= 0)
                {
                    return "{...}";
                }
                Dictionary<string, object> entries = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = ToJson(entry.Value, depth - 1);
                }
                return entries;
            }
            if (value is IEnumerable list)
            {
                return depth <= 0 ? (object)"[...]" : list.Cast<object>().Select(o => ToJson(o, depth - 1)).ToList();
            }
            if (depth <= 0)
            {
                return value.ToString();
            }
            Dictionary<string, object> fields = new Dictionary<string, object>();
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                fields[field.Name] = ToJson(field.GetValue(value), depth - 1);
            }
            return fields;
        }

        private static object FromJson(object json, Type type)
        {
            if (type == typeof(Rect))
            {
                Dictionary<string, object> d = (Dictionary<string, object>)json;
                return new Rect(F(d, "x"), F(d, "y"), F(d, "width"), F(d, "height"));
            }
            if (type == typeof(Vector3))
            {
                Dictionary<string, object> d = (Dictionary<string, object>)json;
                return new Vector3(F(d, "x"), F(d, "y"), F(d, "z"));
            }
            if (type.IsEnum)
            {
                return Enum.Parse(type, Convert.ToString(json, CultureInfo.InvariantCulture));
            }
            return Convert.ChangeType(json, type, CultureInfo.InvariantCulture);
        }

        private static float F(Dictionary<string, object> d, string key)
        {
            return d.ContainsKey(key) ? Convert.ToSingle(d[key], CultureInfo.InvariantCulture) : 0f;
        }
    }
}
