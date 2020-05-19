using Kull.GenericBackend.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.Config
{
    public class ConfigProvider
    {
        private IReadOnlyList<Entity>? entities;
#if NETSTD2
        private readonly IHostingEnvironment hostingEnvironment;
#else
        private readonly IWebHostEnvironment hostingEnvironment;
#endif
        private readonly IConfiguration config;

        public IReadOnlyList<Entity> Entities { get {
                entities = entities ?? ReadConfig();
                return entities; } }

        public ConfigProvider(
#if NETSTD2
            IHostingEnvironment hostingEnvironment,
#else
            IWebHostEnvironment hostingEnvironment,
#endif
                IConfiguration config)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.config = config;
            ReadConfig();
        }

        protected virtual List<Entity> ReadConfig()
        {
            var configFile = System.IO.Path.Combine(hostingEnvironment?.ContentRootPath ?? "", "backendconfig.json");
            bool useConfigFile = System.IO.File.Exists(configFile);
            object configObj =  useConfigFile ?
                (object)ReadJsonFromFile(configFile) :
                config;
            var deepCorrectConfig = (IDictionary<string, object?>)Config.DictionaryHelper.ConvertToDeepIDictionary(configObj, StringComparer.CurrentCultureIgnoreCase)!;
            var ent = (IDictionary<string, object?>)deepCorrectConfig["Entities"]!;
            if(ent == null)
            {
                throw new InvalidOperationException("no config found");
            }
            return ent.Select(s => Entity.GetFromConfig(s.Key, s.Value!)).ToList();
        }

        private IDictionary<string, object> ReadJsonFromFile(string file)
        {
            string json = System.IO.File.ReadAllText(file);
            var target = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
            Newtonsoft.Json.JsonConvert.PopulateObject(json, target);
            return target;
        }

    }
}
