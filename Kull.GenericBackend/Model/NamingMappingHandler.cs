using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Kull.GenericBackend.Model
{
    /// <summary>
    /// A class for handling the mapping between SQL Server Naming Convention and REST Naming Convention (Camel Case)
    /// Handles duplicate field names as well
    /// </summary>
    public class NamingMappingHandler
    {
        public IEnumerable<string> GetNames(IEnumerable<string> dt)
        {
            CamelCaseNamingStrategy strat = new CamelCaseNamingStrategy();
            var setNames = new List<string>();
            int nullCount = 0;
            foreach (var item in dt)
            {
                string name = strat.GetPropertyName(item, false);
                if(name == null)
                {
                    name = "column" + (nullCount == 0 ? "" : nullCount.ToString());
                    nullCount++;
                }
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
