using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Kull.GenericBackend.IntegrationTest
{
    public class MiddlewareTest 
        : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public MiddlewareTest(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/api/Pet?searchString=blub")]
        public async Task GetPets(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            
            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

        }


        [Theory]
        [InlineData("/api/Date?dateParam=")]
        public async Task GetDate(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

            var resp = await response.Content.ReadAsStringAsync();
            var ar = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(resp);
            Assert.Single(ar);
            var obj = (JObject)ar[0];
            Assert.Null(obj.Value<string>("date"));
        }
    }
}
