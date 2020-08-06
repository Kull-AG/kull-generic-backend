using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Kull.GenericBackend.IntegrationTest
{
    public class MiddlewareTestWrap
        : IClassFixture<TestWebApplicationFactoryWrap>
    {
        private readonly TestWebApplicationFactoryWrap _factory;

        public MiddlewareTestWrap(TestWebApplicationFactoryWrap factory)
        {
            _factory = factory;
        }
    }
}
