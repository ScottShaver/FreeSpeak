using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace FreeSpeakWeb.Tests.Services;

/// <summary>
/// Tests for <see cref="ImageUploadService"/> server-side image validation.
/// </summary>
public class ImageUploadServiceTests : IDisposable
{
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "imgsvc-" + Guid.NewGuid().ToString("N"));
    private readonly ImageUploadService _service;

    public ImageUploadServiceTests()
    {
        Directory.CreateDirectory(_root);
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(_root);

        var virusScan = new Mock<IVirusScanService>();
        virusScan.Setup(v => v.ScanAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VirusScanResult.Skipped());

        _service = new ImageUploadService(
            new Mock<IDbContextFactory<ApplicationDbContext>>().Object,
            new Mock<ILogger<ImageUploadService>>().Object,
            env.Object,
            new FileSignatureValidator(new Mock<ILogger<FileSignatureValidator>>().Object),
            virusScan.Object);
    }

    private static string ToDataUrl(byte[] bytes, string contentType) =>
        $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

    [Fact]
    public async Task UploadImagesAsync_TextFileDisguisedAsPng_IsRejectedWithFriendlyError()
    {
        var fake = ToDataUrl(Encoding.UTF8.GetBytes("this is not an image"), "image/png");

        var result = await _service.UploadImagesAsync(
            Guid.NewGuid().ToString(),
            new List<(string, string, string)> { ("fake.png", fake, "image/png") });

        Assert.False(result.Success);
        Assert.Empty(result.ImageUrls);
        Assert.Contains("Invalid image format", result.ErrorMessage);
        Assert.Contains("fake.png", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadImagesAsync_FakePlusValidPng_RejectsWholeBatch()
    {
        var images = new List<(string, string, string)>
        {
            ("good.png", ToDataUrl(ValidPng, "image/png"), "image/png"),
            ("fake.png", ToDataUrl(Encoding.UTF8.GetBytes("not an image"), "image/png"), "image/png"),
        };

        var result = await _service.UploadImagesAsync(Guid.NewGuid().ToString(), images);

        Assert.False(result.Success);
        Assert.Contains("Invalid image format", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadImagesAsync_ValidPng_Succeeds()
    {
        var result = await _service.UploadImagesAsync(
            Guid.NewGuid().ToString(),
            new List<(string, string, string)> { ("good.png", ToDataUrl(ValidPng, "image/png"), "image/png") });

        Assert.True(result.Success);
        Assert.Single(result.ImageUrls);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }
}
