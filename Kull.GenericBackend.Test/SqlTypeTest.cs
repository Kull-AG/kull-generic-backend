using Kull.GenericBackend.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Kull.DatabaseMetadata;

namespace Kull.GenericBackend.Test
{
    [TestClass]
    public class SqlTypeTest
    {
        [TestMethod]
        public void TestNvarchar()
        {
            SqlType sqlType = SqlType.GetSqlType("nvarchar(MAX)");
            Assert.AreEqual("nvarchar", sqlType.DbType);
            Assert.AreEqual("string", sqlType.JsType);
            Assert.AreEqual(typeof(string), sqlType.NetType);
        }
    }
}
