using Kull.GenericBackend.GenericSP;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Kull.GenericBackend.Test
{
    [TestClass]
    public class NamingTest
    {
        [TestMethod]
        public void TestByIdAndSecond()
        {
            var ent = new Entity("/Cases/{CaseId:int}/Brand", new Dictionary<string, Method>());
            var displayString = ent.GetDisplayString();
            Assert.AreEqual(displayString, "CaseBrand");
        }

        [TestMethod]
        public void TestById()
        {
            var ent = new Entity("/Cases/{CaseId:int}", new Dictionary<string, Method>());
            var displayString = ent.GetDisplayString();
            Assert.AreEqual(displayString, "Case");
        }

        [TestMethod]
        public void TestResource()
        {
            var ent = new Entity("/Cases", new Dictionary<string, Method>());
            var displayString = ent.GetDisplayString();
            Assert.AreEqual(displayString, "Cases");
        }
    }
}
