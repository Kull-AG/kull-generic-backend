using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Threading.Tasks;
using Xunit;

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
            // TODO: Check result
        }
    }
}
