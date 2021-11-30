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
    public class MiddlewareTestWrap
        : IClassFixture<TestWebApplicationFactory<TestStartupWrap>>
    {
        private readonly TestWebApplicationFactory<TestStartupWrap> _factory;



        public MiddlewareTestWrap(TestWebApplicationFactory<TestStartupWrap> factory)
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
            var getContent = await response.Content.ReadAsStringAsync();
            var asDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(getContent);
            var valueString = Newtonsoft.Json.JsonConvert.SerializeObject(asDict["value"]);
            var asDictList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(valueString);

            var withoutTs = asDictList.Select(d => d.Keys.Where(k => k != "ts" && k != "description").ToDictionary(k => k, k => d[k])).ToArray();
            Utils.JsonUtils.AssertJsonEquals(new[]
            {
                new
                {
                    petId=1,
                    petName="Dog",
                    isNice=false
                },
                new
                {
                    petId=2,
                    petName= "Dog 2 with \" in name \r\nand a newline ä$¨^ `",
                    isNice =true
                }
            }, withoutTs);

            Console.WriteLine("RAM: " + GC.GetTotalMemory(false).ToString("#,0"));
        }

        [Theory]
        [InlineData("/rest/Dog/1")]
        public async Task UpdateDog(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var putParameter = Newtonsoft.Json.JsonConvert.SerializeObject(
                new { ts = new byte[] { (byte)0x34 }, DogId=1 });

            var putResponse = await client.PutAsync(url,
                    new System.Net.Http.StringContent(putParameter));
            var putContent = await putResponse.Content.ReadAsStringAsync();
            putResponse.EnsureSuccessStatusCode();
            Utils.JsonUtils.AssertJsonEquals(putContent, new
            {
                @out = new
                {
                    ts = Convert.ToBase64String(new byte[] { 1 })
                },
                value = new string[] {}
            });
            
        }
    }
}
