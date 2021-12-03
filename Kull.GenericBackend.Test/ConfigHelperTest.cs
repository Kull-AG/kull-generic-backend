using Kull.GenericBackend.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.Test
{
    [TestClass]
    public class ConfigHelperTest
    {
        [TestMethod]
        public void TestConfigHelper()
        {
            string json = @"{
    ""searchString"": """",
    ""employeeAreaId"": null,
    ""skip"": 0,
    ""take"": 60,
    ""includeTotal"": true,
    ""sortingSQL"": ""stationCode,stationAddress"",
    ""filter"": {
                ""logic"": ""and"",
        ""filters"": [{
                    ""field"": ""stationZipCode"",
                ""operator"": ""eq"",
                ""value"": ""5000""
            }
        ]
    }
        }
";
            var retValue = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var parameterObject = retValue!.ToDictionary(k => k.Key, v => Config.DictionaryHelper.ConvertToDeepIDictionary(v.Value, StringComparer.CurrentCultureIgnoreCase), StringComparer.CurrentCultureIgnoreCase)!;
            Assert.IsNotNull(parameterObject["filter"]);
            var filterAsDict = parameterObject["filter"] as IReadOnlyDictionary<string, object?>;
            Assert.IsNotNull(filterAsDict);
            Assert.IsNotNull(filterAsDict["filters"]);
            var filtersAsArray = filterAsDict["filters"] as IEnumerable<object>;
            Assert.IsNotNull(filtersAsArray);
            Assert.AreEqual(1, filtersAsArray.Count());
            var firstItem = filtersAsArray.First();
            var firstItemAsObj = firstItem as IReadOnlyDictionary<string, object?>;
            Assert.IsNotNull(firstItemAsObj);
            Assert.AreEqual("stationZipCode", firstItemAsObj["field"]);

        }
    }
}
