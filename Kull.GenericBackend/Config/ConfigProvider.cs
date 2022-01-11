using Kull.GenericBackend.Common;
#if NETFX 
using Kull.MvcCompat;
#else 
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
#endif
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kull.GenericBackend.Config;

public class ConfigProvider
{
    private IReadOnlyList<Entity>? entities;
#if NETSTD2 || NETFX
    private readonly IHostingEnvironment hostingEnvironment;
#else
    private readonly IWebHostEnvironment hostingEnvironment;
#endif
#if !NETFX
    private readonly IConfiguration config;
#endif
    public IReadOnlyList<Entity> Entities
    {
        get
        {
            entities = entities ?? ReadConfig();
            return entities;
        }
    }

    public ConfigProvider(
#if NETSTD2 || NETFX
            IHostingEnvironment hostingEnvironment
#else
            IWebHostEnvironment hostingEnvironment
#endif
#if !NETFX
                , IConfiguration config
#endif
            )
    {
        this.hostingEnvironment = hostingEnvironment;
#if !NETFX
        this.config = config;
#endif
        ReadConfig();
    }

    protected virtual List<Entity> ReadConfig()
    {
        var configFile = System.IO.Path.Combine(hostingEnvironment?.ContentRootPath ?? "", "backendconfig.json");
#if NETFX
        object? config = null;
#endif
        bool useConfigFile = System.IO.File.Exists(configFile);
        object? configObj = useConfigFile ?
            (object)ReadJsonFromFile(configFile) :
            config;
        if (configObj == null)
        {
            throw new InvalidOperationException("no config file found");
        }
        var deepCorrectConfig = (IReadOnlyDictionary<string, object?>)Config.DictionaryHelper.ConvertToDeepIDictionary(configObj, StringComparer.CurrentCultureIgnoreCase)!;
        var ent = (IReadOnlyDictionary<string, object?>)deepCorrectConfig["Entities"]!;
        if (ent == null)
        {
            throw new InvalidOperationException("no config found");
        }
        return ent.Select(s => Entity.GetFromConfig(s.Key, s.Value!)).ToList();
    }

    private IReadOnlyDictionary<string, object> ReadJsonFromFile(string file)
    {
        string json = System.IO.File.ReadAllText(file);
#if NEWTONSOFTJSON
        var target = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
        Newtonsoft.Json.JsonConvert.PopulateObject(json, target);
#else
        var target1 = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        var target = new Dictionary<string, object>(target1!, StringComparer.CurrentCultureIgnoreCase);
#endif
        return target;
    }

}
