#if !NETFX
using Microsoft.Extensions.Configuration;
#endif
#if NEWTONSOFTJSON
using Newtonsoft.Json.Linq;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Config;

public static class DictionaryHelper
{
    public static T GetValue<T>(this IReadOnlyDictionary<string, object?> dictionary, string key)
    {

        if (dictionary.ContainsKey(key))
        {
            var value = dictionary[key]!;
            if (value == null) return default(T)!;
            if (value is T t) return t;
            if (typeof(T) == typeof(IReadOnlyDictionary<string, object>))
            {
                return (T)(object)ConvertToDeepIDictionary(value, StringComparer.CurrentCultureIgnoreCase)!;
            }
            if (typeof(T) == typeof(IReadOnlyCollection<string>))
            {
#if NEWTONSOFTJSON
                if (value is JArray ar)
                {
                    return (T)(object)ar.Children().Select(s => s.Value<string>()).ToList();
                }
#else
                if (false) { }
#endif
                else if (value is IEnumerable<string> es)
                {
                    return (T)(object)es.ToList();
                }
                else if (value is IEnumerable<object> es2)
                {
                    return (T)(object)es2.Select(s => s.ToString()).ToList();
                }
                else if (value is string s)
                {
                    return (T)(object)new string[] { s };
                }
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


    public static object? ConvertToDeepIDictionary(object? input, StringComparer stringComparer)
    {
        if (input == null) return null;
#if NEWTONSOFTJSON
        if (input is JObject obj)
        {
            return obj.Properties().ToDictionary(o => o.Name, obj => ConvertToDeepIDictionary(obj.Value, stringComparer), stringComparer);
        }
        if (input is JArray ar)
        {
            return ar.Children().Select(c => ConvertToDeepIDictionary(c, stringComparer)).ToArray();
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
#endif
#if !NETFX
        if (input is System.Text.Json.JsonElement e)
        {
            switch (e.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Undefined:
                    return null;
                case System.Text.Json.JsonValueKind.Object:
                    return e.EnumerateObject().ToDictionary(p => p.Name, p => ConvertToDeepIDictionary(p.Value, stringComparer), stringComparer);
                case System.Text.Json.JsonValueKind.Array:
                    return e.EnumerateArray().Select(p => ConvertToDeepIDictionary(p, stringComparer)).ToArray();
                case System.Text.Json.JsonValueKind.String:
                    return e.GetString();
                case System.Text.Json.JsonValueKind.Number:
                    return e.GetDouble();
                case System.Text.Json.JsonValueKind.True:
                    return true;
                case System.Text.Json.JsonValueKind.False:
                    return false;
                case System.Text.Json.JsonValueKind.Null:
                    return null;
                default:
                    break;
            }
        }
#endif
        if (input is IReadOnlyDictionary<string, object> dict)
        {
            return dict.ToDictionary(o => o.Key, o => ConvertToDeepIDictionary(o.Value, stringComparer), stringComparer);
        }
#if !NETFX
        if (input is IConfigurationRoot config)
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
