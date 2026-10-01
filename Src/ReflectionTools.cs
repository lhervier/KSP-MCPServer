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

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("get_member",
                "Reads a field or property of the first loaded object of a type (its full or short name), or a " +
                "static one. Numbers, strings, booleans, vectors and rects come back as JSON; lists as their items.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("member", "string", "field or property name", true)),
                GetMember);
            yield return new Tool("set_member",
                "Writes a field or property of the first loaded object of a type, or a static one. The value is a " +
                "number, string or boolean, or an object {x, y, width, height} for a Rect, {x, y, z} for a vector.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("member", "string", "field or property name", true),
                    Schema.P("value", "object", "the new value", true)),
                SetMember);
            yield return new Tool("call_method",
                "Calls a method without arguments of the first loaded object of a type, or a static one, and " +
                "returns what it returns.",
                Schema.Object(
                    Schema.P("type", "string", "type name, full or short", true),
                    Schema.P("method", "string", "method name", true)),
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
            return typeof(Vessel).Assembly.GetType(name) ?? typeof(Vessel).Assembly.GetTypes().FirstOrDefault(t => t.Name == name);
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
            target = UnityEngine.Object.FindObjectOfType(type);
            if (target == null)
            {
                call.Fail("No loaded " + type.FullName);
                return false;
            }
            return true;
        }

        private static IEnumerator GetMember(ToolCall call)
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
            object value = member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target, null);
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
