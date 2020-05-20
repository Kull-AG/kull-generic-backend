#if !NETFX
using Microsoft.Extensions.Configuration;
#endif
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Config
{
    internal static class DictionaryHelper
    {
        public static T GetValue<T>(this IDictionary<string, object?> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                var value  = dictionary[key]!;
                if (value == null) return default(T)!;
                if (value is T t)
                {
                    return t;
                }
                else if (value is IConvertible c)
                {
                    // Required for appsettings.json where everything is a string
                    if (typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        return (T)c.ToType(Nullable.GetUnderlyingType(typeof(T))!, null);
                    }
                    return (T)c.ToType(typeof(T), null);
                }
                else
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            return default(T)!;
        }


        internal static object? ConvertToDeepIDictionary(object input, StringComparer stringComparer)
        {
            if (input == null) return null;
            if (input is JObject obj)
            {
                return obj.Properties().ToDictionary(o => o.Name, obj => ConvertToDeepIDictionary(obj.Value, stringComparer), stringComparer);
            }
            if (input is JArray ar)
            {
                return ar.Children().Select(c => ConvertToDeepIDictionary(c, stringComparer));
            }
            if (input is JToken tk)
            {
                switch (tk.Type)
                {
                    case JTokenType.Integer:
                        return tk.Value<int>();
                    case JTokenType.Float:
                        return tk.Value<float>();
                    case JTokenType.String:
                        return tk.Value<string>();
                    case JTokenType.Boolean:
                        return tk.Value<bool>();
                    case JTokenType.Null:
                        return null;
                    case JTokenType.Uri:
                        return tk.Value<string>();
                    default:
                        throw new NotSupportedException("Cannot convert Json");
                }
            }
            if (input is IDictionary<string, object> dict)
            {
                return dict.ToDictionary(o => o.Key, o => ConvertToDeepIDictionary(o.Value, stringComparer), stringComparer);
            }
#if !NETFX
            if (input is ConfigurationRoot config)
            {
                return new Dictionary<string, object?>(){
                    {"Entities", ConvertToDeepIDictionary(config.GetSection("Entities"),stringComparer) } };
            }
            if (input is IConfigurationSection section)
            {
                if (section.Value != null) return section.Value;
                var children = section.GetChildren().ToArray();
                if (children.Length == 0) return null;
                // It's an array
                if (children[0].Key == null) return children.Select(c => ConvertToDeepIDictionary(c, stringComparer)).ToArray();
                // It's an object
                return children.ToDictionary(o => o.Key, o => ConvertToDeepIDictionary(o, stringComparer), stringComparer);
            }
#endif
            return input;
        }
    }
}
