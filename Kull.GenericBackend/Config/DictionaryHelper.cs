using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.Config
{
    internal static class DictionaryHelper
    {
        public static T GetValue<T>(this IDictionary<string, object?> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
            {
                return (T)dictionary[key]!;
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
            if (input is IConfiguration config)
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
            return input;
        }
    }
}
