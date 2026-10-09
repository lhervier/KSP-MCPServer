using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that other mods declare, without depending on this one. A mod defines its own attribute class
    /// named <c>McpToolAttribute</c>, with <c>Name</c> and <c>Description</c> properties, and puts it on
    /// the methods it offers; this server finds them by the attribute's name when KSP starts, and publishes
    /// each as a tool.
    /// </summary>
    /// <remarks>
    /// A method may be static or not, public or not. Its parameters become the tool's arguments, by name:
    /// numbers, strings and booleans. An instance method is called on the loaded object of its type, so
    /// that type has to be a Unity object, typically the mod's MonoBehaviour. What the method returns is
    /// written as JSON: numbers, strings, booleans, lists, dictionaries, and the public fields of other
    /// objects.
    /// </remarks>
    internal static class ExtensionTools
    {
        private const string AttributeName = "McpToolAttribute";
        private const BindingFlags AnyMethod = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>The tools declared by the loaded assemblies, this one apart.</summary>
        public static IEnumerable<Tool> All()
        {
            List<Tool> tools = new List<Tool>();
            foreach (AssemblyLoader.LoadedAssembly loaded in AssemblyLoader.loadedAssemblies)
            {
                if (loaded.assembly == typeof(ExtensionTools).Assembly)
                {
                    continue;
                }
                foreach (Type type in TypesOf(loaded.assembly))
                {
                    foreach (MethodInfo method in type.GetMethods(AnyMethod))
                    {
                        object attribute = method.GetCustomAttributes(false)
                            .FirstOrDefault(a => a.GetType().Name == AttributeName);
                        if (attribute == null)
                        {
                            continue;
                        }
                        string name = Property(attribute, "Name");
                        if (string.IsNullOrEmpty(name))
                        {
                            Log.Error("Ignored " + type.FullName + "." + method.Name + ": its " + AttributeName + " has no Name");
                            continue;
                        }
                        tools.Add(Make(name, Property(attribute, "Description") ?? "", method));
                        Log.Info("Tool " + name + " from " + loaded.name + " (" + type.FullName + "." + method.Name + ")");
                    }
                }
            }
            return tools;
        }

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private static string Property(object attribute, string name)
        {
            PropertyInfo property = attribute.GetType().GetProperty(name);
            if (property != null)
            {
                return property.GetValue(attribute, null) as string;
            }
            FieldInfo field = attribute.GetType().GetField(name);
            return field != null ? field.GetValue(attribute) as string : null;
        }

        private static Tool Make(string name, string description, MethodInfo method)
        {
            List<Schema.Prop> props = new List<Schema.Prop>();
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                props.Add(Schema.P(parameter.Name, JsonType(parameter.ParameterType), parameter.ParameterType.Name,
                    !parameter.IsOptional));
            }
            return new Tool(name, description, Schema.Object(props.ToArray()), call => Run(call, method)) { Concurrent = true };
        }

        private static string JsonType(Type type)
        {
            if (type == typeof(bool))
            {
                return "boolean";
            }
            if (type == typeof(string))
            {
                return "string";
            }
            return type.IsPrimitive ? "number" : "object";
        }

        private static IEnumerator Run(ToolCall call, MethodInfo method)
        {
            object target = null;
            if (!method.IsStatic)
            {
                target = typeof(UnityEngine.Object).IsAssignableFrom(method.DeclaringType)
                    ? UnityEngine.Object.FindObjectOfType(method.DeclaringType)
                    : null;
                if (target == null)
                {
                    call.Fail("No loaded " + method.DeclaringType.FullName + " to call " + method.Name + " on, in this scene");
                    yield break;
                }
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] values = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (call.Has(parameter.Name))
                {
                    values[i] = Convert.ChangeType(call.Arguments[parameter.Name], parameter.ParameterType, CultureInfo.InvariantCulture);
                }
                else if (parameter.IsOptional)
                {
                    values[i] = parameter.DefaultValue;
                }
                else
                {
                    call.Fail("Missing argument " + parameter.Name);
                    yield break;
                }
            }

            object result;
            try
            {
                result = method.Invoke(target, values);
            }
            catch (TargetInvocationException e)
            {
                call.Fail(method.Name + " threw: " + e.InnerException?.Message);
                yield break;
            }
            call.Text(method.ReturnType == typeof(void) ? (object)"done" : new Dictionary<string, object>
            {
                { "returned", ReflectionTools.ToJson(result, 6) }
            });
        }
    }
}
