using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// A class for handling the mapping between SQL Server Naming Convention and REST Naming Convention (Camel Case)
    /// Handles duplicate field names as well
    /// </summary>
    public class NamingMappingHandler
    {
        public void SetNames(IEnumerable<ISqlMappedData> dt)
        {
            CamelCaseNamingStrategy strat = new CamelCaseNamingStrategy();
            var setNames = new List<string>();
            
            foreach (var item in dt)
            {
                if (item.WebApiName != null)
                {
                    string name = strat.GetPropertyName(item.SqlName, false);
                    var origName = name;
                    int i = 1;
                    while (setNames.Contains(name))
                    {
                        name = origName + "_" + i.ToString();
                    }
                    setNames.Add(name);
                    item.WebApiName = name;
                }
            }
        }


        public IEnumerable<string> GetNames(IEnumerable<string> dt)
        {
            CamelCaseNamingStrategy strat = new CamelCaseNamingStrategy();
            var setNames = new List<string>();
            foreach (var item in dt)
            {
                string name = strat.GetPropertyName(item, false);
                var origName = name;
                int i = 1;
                while (setNames.Contains(name))
                {
                    name = origName + "_" + i.ToString();
                }
                setNames.Add(name);
                yield return name;
            }
        }
    }
}
