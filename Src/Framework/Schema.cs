using System.Collections.Generic;

namespace com.github.lhervier.ksp.mcpserver
{
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
