using Kull.GenericBackend.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Kull.GenericBackend.IntegrationTest
{
    public class TestStartupAuth : TestStartupBase
    {
        protected override bool UseSwaggerV2 => false;

        protected override void ConfigureMiddleware(SPMiddlewareOptions options)
        {
            base.ConfigureMiddleware(options);
            options.RequireAuthenticated = true;
        }
    }
    public class MiddlewareTestAuth
        : IClassFixture<TestWebApplicationFactory<TestStartupAuth>>
    {
        private readonly TestWebApplicationFactory<TestStartupAuth> _factory;



        public MiddlewareTestAuth(TestWebApplicationFactory<TestStartupAuth> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/rest/Pet?searchString=blub")]
        public async Task GetPetUnauthenticated(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

            // Act
            var response = await client.GetAsync(url);

            // Assert
            Assert.Equal(401, ((int)response.StatusCode)); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);
            
        }

    }
}
