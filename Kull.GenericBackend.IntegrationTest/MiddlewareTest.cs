using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        [InlineData("/rest/Pet?searchString=blub")]
        public async Task GetPets(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

        }
        [Theory]
        [InlineData("/rest/Pet")]
        public async Task UpdatePetWithoutTimestamp(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var putParameter = Newtonsoft.Json.JsonConvert.SerializeObject(
                new { petId = 1, ts = (byte[])null });

            var putResponse = await client.PutAsync(url,
                    new System.Net.Http.StringContent(putParameter));
            var putContent = await putResponse.Content.ReadAsStringAsync();
            putResponse.EnsureSuccessStatusCode();
            var resultPut = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(putContent);
            Assert.Empty(resultPut);
        }


        [Theory]
        [InlineData("/rest/Pet")]
        public async Task UpdatePetWithTimestamp(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            var ar = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(content);
            Assert.NotEmpty(ar);
            var obj = (JObject)ar[0];
            var petId = obj.Value<int>("petId");
            var timeStamp = obj.Value<string>("ts");
            Assert.NotEqual("System.Byte[]", timeStamp);
            Assert.NotNull(timeStamp);

            var putParameter = Newtonsoft.Json.JsonConvert.SerializeObject(
                new { petId, ts = timeStamp });

            var putResponse = await client.PutAsync(url,
                    new System.Net.Http.StringContent(putParameter));
            var putContent = await putResponse.Content.ReadAsStringAsync();
            putResponse.EnsureSuccessStatusCode();
            var resultPut = Newtonsoft.Json.JsonConvert.DeserializeObject<JArray>(putContent);
            Assert.Single(resultPut);
        }

        [Theory]
        [InlineData("/rest/Pet?searchString=blub")]
        public async Task GetPetsXml(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/xml",
                response.Content.Headers.ContentType.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            var xml = System.Xml.Linq.XElement.Parse(content);
        }

        [Theory]
        [InlineData("/rest/htcp")]
        public async Task TestRequestInterceptor(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(418, (int)response.StatusCode);
            Assert.Equal("text/plain",
                response.Content.Headers.ContentType.MediaType);
            Assert.Equal("Hey, I am a teapot", content);
        }

        [Theory]
        [InlineData("/rest/Pet?searchString=blub")]
        public async Task GetPetsXHtml(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));

            // Act
            var response = await client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/xhtml+xml",
                response.Content.Headers.ContentType.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            var xml = System.Xml.Linq.XElement.Parse(content);
        }



        [Theory]
        [InlineData("/rest/Date?dateParam=")]
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



        [Theory]
        [InlineData("/rest/Confidential")]
        public async Task GetNotPermittedConfidential(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // User Error
            Assert.InRange((int)response.StatusCode, 400, 499);
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

            var resp = await response.Content.ReadAsStringAsync();
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(resp);

        }


        [Theory]
        [InlineData("/rest/Confidential")]
        public async Task GetNotPermittedConfidentialXml(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));

            // Act
            var response = await client.GetAsync(url);

            // User Error
            Assert.InRange((int)response.StatusCode, 400, 499);
            Assert.Contains("xml", response.Content.Headers.ContentType.MediaType);

            var resp = await response.Content.ReadAsStringAsync();
            XElement e = XElement.Parse(resp);

        }


        [Theory]
        [InlineData("/rest/Bug")]
        public async Task TestBuggyApi(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync(url);

            // App Error
            Assert.InRange((int)response.StatusCode, 500, 599);
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

            var resp = await response.Content.ReadAsStringAsync();
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(resp);

        }
    }
}
