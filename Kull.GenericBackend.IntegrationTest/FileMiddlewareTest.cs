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

namespace Kull.GenericBackend.IntegrationTest;

public class FileMiddlewareTest
    : IClassFixture<TestWebApplicationFactory<TestStartup>>
{
    private readonly TestWebApplicationFactory<TestStartup> _factory;

    public FileMiddlewareTest(TestWebApplicationFactory<TestStartup> factory
        )
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/rest/File")]
    public async Task UploadFile(string url)
    {
        // Arrange
        var client = _factory.CreateClient();

        using (var formContent = new MultipartFormDataContent("NKdKd9Yk"))
        {
            formContent.Headers.ContentType.MediaType = "multipart/form-data";
            // 3. Add the filename C:\\... + fileName is the path your file
            Stream fileStream = System.IO.File.OpenRead("sampleImage.jpg");
            formContent.Add(new StreamContent(fileStream), "image", "sampleImage.jpg");
            formContent.Add(new StringContent("tester"), "FileDesc");


            // 4.. Execute the MultipartPostMethod
            var message = await client.PostAsync(url, formContent);
            // 5.a Receive the response
            message.EnsureSuccessStatusCode();
            var img = await message.Content.ReadAsByteArrayAsync();
            using var ts = SixLabors.ImageSharp.Image.Load(img);
            Assert.Equal(360, ts.Height);
            Assert.Equal(640, ts.Width);
        }


    }

    [Theory]
    [InlineData("/rest/FileNoFn")]
    public async Task UploadFileNoFn(string url)
    {
        // Arrange
        var client = _factory.CreateClient();

        using (var formContent = new MultipartFormDataContent("NKdKd9Yk"))
        {
            formContent.Headers.ContentType.MediaType = "multipart/form-data";
            // 3. Add the filename C:\\... + fileName is the path your file
            Stream fileStream = System.IO.File.OpenRead("sampleImage.jpg");
            formContent.Add(new StreamContent(fileStream), "image", "sampleImage.jpg");
            formContent.Add(new StringContent("tester"), "FileDesc");


            // 4.. Execute the MultipartPostMethod
            var message = await client.PostAsync(url, formContent);
            // 5.a Receive the response
            message.EnsureSuccessStatusCode();
            var img = await message.Content.ReadAsByteArrayAsync();
            using var ts = SixLabors.ImageSharp.Image.Load(img);
            Assert.Equal(360, ts.Height);
            Assert.Equal(640, ts.Width);
        }


    }
}
