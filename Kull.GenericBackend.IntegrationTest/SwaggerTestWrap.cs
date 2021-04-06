using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;

namespace Kull.GenericBackend.IntegrationTest
{
    public class SwaggerTestWrap
        : IClassFixture<TestWebApplicationFactoryWrap>
    {
        private readonly TestWebApplicationFactoryWrap _factory;

        public SwaggerTestWrap(TestWebApplicationFactoryWrap factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/swagger/v1/swagger.json")]
        public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json; charset=utf-8",
                response.Content.Headers.ContentType.ToString());
            var resp = await response.Content.ReadAsStringAsync();
            System.IO.File.WriteAllText("testWrap.json", resp);
            var jObj = JsonConvert.DeserializeObject<JObject>(resp);
            var petParameter = (JArray)jObj["paths"]["/rest/Pet"]["get"]["parameters"];
            Assert.Equal(2, petParameter.Count);
            var onlyNiceParam =
                petParameter.Children<JObject>()
                .Single(p => p.Value<string>("name") == "onlyNice");
            Assert.Equal("boolean", onlyNiceParam["schema"].Value<string>("type"));


            var searchStringParam =
                petParameter.Children<JObject>()
                .Single(p => p.Value<string>("name") == "searchString");
            Assert.Equal("string", searchStringParam["schema"].Value<string>("type"));


            var postAsGetOp = (JObject)jObj["paths"]["/rest/Test"]["post"];
            string opId = postAsGetOp.Value<string>("operationId");
            Assert.Equal("GetBackend", opId);

            /*
            var testResult = (JObject)jObj["paths"]["/api/Test"]["patch"];
            string opId = testResult.Value<string>("operationId");
            Assert.Equal("GetBackend", opId);*/
        }



        [Theory]
        [InlineData("/swagger/v1/swagger.json")]
        public async Task Get_TestTempTable(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json; charset=utf-8",
                response.Content.Headers.ContentType.ToString());
            var resp = await response.Content.ReadAsStringAsync();
            System.IO.File.WriteAllText("testV3.json", resp);
            var document = new Microsoft.OpenApi.Readers.OpenApiStringReader().Read(resp, out var apiDiagnostic);
            Assert.Empty(apiDiagnostic.Errors);
            Assert.Equal(OpenApiSpecVersion.OpenApi3_0, apiDiagnostic.SpecificationVersion);
            var testTempPath = document.Paths["/rest/TestTemp"].Operations[OperationType.Get];
            
            Assert.Single(testTempPath.Parameters);
            Assert.Equal("anAwesomeParam", testTempPath.Parameters.First().Name);
            
            /*
            var testResult = (JObject)jObj["paths"]["/api/Test"]["patch"];
            string opId = testResult.Value<string>("operationId");
            Assert.Equal("GetBackend", opId);*/
        }
    }
}
