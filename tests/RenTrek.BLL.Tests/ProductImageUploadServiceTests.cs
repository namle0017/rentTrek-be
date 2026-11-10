using System.Net;
using System.Security.Cryptography;
using System.Text;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;

namespace RenTrek.BLL.Tests;

public sealed class ProductImageUploadServiceTests
{
    [Fact]
    public async Task SignsUploadParametersWithoutSendingTheSecret()
    {
        using var handler = new UploadHandler();
        using var http = new HttpClient(handler);
        var service = new ProductImageUploadService(Microsoft.Extensions.Options.Options.Create(new CloudinarySettings
        {
            CloudName = "test-cloud",
            ApiKey = "<test-key>",
            ApiSecret = "test-secret"
        }), http);
        using var stream = new MemoryStream([1, 2, 3]);
        var url = await service.UploadAsync(stream, "photo.png", "image/png", 3);
        Assert.Equal("https://res.cloudinary.com/test-cloud/image/upload/test.png", url);
        Assert.Equal("https://api.cloudinary.com/v1_1/test-cloud/image/upload", handler.Url);
        var fields = handler.Fields;
        Assert.StartsWith("rentrek/products/", fields["public_id"]);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"public_id={fields["public_id"]}&timestamp={fields["timestamp"]}test-secret"))).ToLowerInvariant();
        Assert.Equal(expected, fields["signature"]);
        Assert.DoesNotContain(fields.Values, value => value.Contains("test-secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("photo.svg", "image/svg+xml", 3)]
    [InlineData("photo.png", "image/jpeg", 3)]
    [InlineData("photo.png", "image/png", 0)]
    [InlineData("photo.png", "image/png", 5242881)]
    public async Task RejectsInvalidImagesBeforeCallingCloudinary(string name, string type, long length)
    {
        using var handler = new UploadHandler();
        using var http = new HttpClient(handler);
        var service = new ProductImageUploadService(Microsoft.Extensions.Options.Options.Create(new CloudinarySettings()), http);
        using var stream = new MemoryStream([1]);
        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(stream, name, type, length));
        Assert.Null(handler.Url);
    }

    [Fact]
    public async Task ReportsMissingConfigurationWithoutAnExternalRequest()
    {
        using var handler = new UploadHandler();
        using var http = new HttpClient(handler);
        var service = new ProductImageUploadService(Microsoft.Extensions.Options.Options.Create(new CloudinarySettings()), http);
        using var stream = new MemoryStream([1]);
        await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(stream, "photo.png", "image/png", 1));
        Assert.Null(handler.Url);
    }

    private sealed class UploadHandler : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public Dictionary<string, string> Fields { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            foreach (var field in (MultipartFormDataContent)request.Content!)
            {
                var name = field.Headers.ContentDisposition!.Name!.Trim('"');
                if (name != "file") Fields[name] = await field.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"secure_url\":\"https://res.cloudinary.com/test-cloud/image/upload/test.png\"}")
            };
        }
    }
}
