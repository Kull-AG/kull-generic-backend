using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Kull.GenericBackend.Test
{
    [TestClass]
    public class AppSettingsTest
    {
        [TestMethod]
        public void TestAppSettings()
        {
            var builder = new ConfigurationBuilder()
                        .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var config = builder.Build();
            var provider = new Kull.GenericBackend.Config.ConfigProvider(null, config);
            Assert.AreEqual(8, provider.Entities.Count);
            var petSearch = provider.Entities.First(e => e.ToString().StartsWith("/Pet/search", StringComparison.CurrentCultureIgnoreCase));
            Assert.AreEqual(1,petSearch.Methods.Count) ;
            Assert.AreEqual("spSearchPets", petSearch.Methods[Microsoft.OpenApi.Models.OperationType.Get].SP);
            Assert.AreEqual(360, petSearch.Methods[Microsoft.OpenApi.Models.OperationType.Get].CommandTimeout);
        }
    }
}
