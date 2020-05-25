using Kull.GenericBackend.GenericSP;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Kull.GenericBackend.Common
{
    /// <summary>
    /// A class for handling the mapping between SQL Server Naming Convention and REST Naming Convention (Camel Case)
    /// Handles duplicate field names as well
    /// </summary>
    public class NamingMappingHandler
    {
        private readonly SPMiddlewareOptions options;

        public NamingMappingHandler(SPMiddlewareOptions options)
        {
            this.options = options;
        }

        public IEnumerable<string> GetNames(IEnumerable<string> dt)
        {
            var setNames = new List<string>();
            int nullCount = 0;
            foreach (var item in dt)
            {
                string name = options.NamingStrategy.GetPropertyName(item, false);
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
