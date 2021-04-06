using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace Kull.GenericBackend.IntegrationTest
{
    public class SwaggerTest
        : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public SwaggerTest(TestWebApplicationFactory factory)
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
            System.IO.File.WriteAllText("test.json", resp);
            var jObj = JsonConvert.DeserializeObject<JObject>(resp);
            var petParameter = (JArray)jObj["paths"]["/rest/Pet"]["get"]["parameters"];
            Assert.Equal(2, petParameter.Count);
            var onlyNiceParam =
                petParameter.Children<JObject>()
                .Single(p => p.Value<string>("name") == "onlyNice");
            Assert.Equal("boolean", onlyNiceParam.Value<string>("type"));
            if (onlyNiceParam.TryGetValue("required", out var token))
            {
                var vl = (bool)((JValue)token).Value;
                Assert.False(vl);
            }

            var searchStringParam =
                petParameter.Children<JObject>()
                .Single(p => p.Value<string>("name") == "searchString");
            Assert.Equal("string", searchStringParam.Value<string>("type"));


            var postAsGetOp = (JObject)jObj["paths"]["/rest/Test"]["post"];
            string opId = postAsGetOp.Value<string>("operationId");
            Assert.Equal("GetBackend", opId);

            /*
            var testResult = (JObject)jObj["paths"]["/api/Test"]["patch"];
            string opId = testResult.Value<string>("operationId");
            Assert.Equal("GetBackend", opId);*/
        }
    }


}
