using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Kull.GenericBackend.IntegrationTest
{
    public class FileMiddlewareTest
        : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
#if NETSTD2
        private readonly IHostingEnvironment webHostEnvironment;
#else
           private readonly  IWebHostEnvironment webHostEnvironment;
#endif
        public FileMiddlewareTest(TestWebApplicationFactory factory,
#if NETSTD2
            IHostingEnvironment webHostEnvironment
#else
            IWebHostEnvironment webHostEnvironment
#endif
            )
        {
            _factory = factory;
            this.webHostEnvironment = webHostEnvironment;
        }

        [Theory]
        [InlineData("/api/File")]
        public async Task GetPets(string url)
        {
            // Arrange
            var client = _factory.CreateClient();

            string result;
            using (var formContent = new MultipartFormDataContent("NKdKd9Yk"))
            {
                formContent.Headers.ContentType.MediaType = "multipart/form-data";
                // 3. Add the filename C:\\... + fileName is the path your file
                Stream fileStream = System.IO.File.OpenRead(System.IO.Path.Combine(webHostEnvironment.ContentRootPath, "sampleImage.jpg"));
                formContent.Add(new StreamContent(fileStream), "image", "sampleImage.jpg");



                // 4.. Execute the MultipartPostMethod
                var message = await client.PostAsync(url, formContent);
                // 5.a Receive the response
                message.EnsureSuccessStatusCode();
                result = await message.Content.ReadAsStringAsync();


            }

            // Act
            var response = await client.PostAsync(url,
                new MultipartFormDataContent()
                {
                }
                );

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json",
                response.Content.Headers.ContentType.MediaType);

        }
    }
}
