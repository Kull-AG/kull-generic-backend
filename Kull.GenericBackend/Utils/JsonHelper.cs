using Kull.GenericBackend.Middleware;
#if NEWTONSOFTJSON
using Newtonsoft.Json.Serialization;
#endif
using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.Utils;

internal class JsonHelper
{
    public static string SerializeObject(object obj, SPMiddlewareOptions? settings = null)
    {
#if NEWTONSOFTJSON
        if (settings == null)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }
        DefaultContractResolver contractResolver = new DefaultContractResolver
        {
            NamingStrategy = settings.NamingStrategy
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(obj, new Newtonsoft.Json.JsonSerializerSettings()
        {
            ContractResolver = contractResolver
        });
#else
        return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions()
        {
            PropertyNamingPolicy = settings == null ? System.Text.Json.JsonNamingPolicy.CamelCase: settings?.NamingStrategy
        });
#endif
    }
    public static object DeserializeObject(string json)
    {
#if NEWTONSOFTJSON
        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JToken>(json);
#else
        var obj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
#endif
        return Config.DictionaryHelper.ConvertToDeepIDictionary(obj, StringComparer.InvariantCultureIgnoreCase)!;
    }
}
