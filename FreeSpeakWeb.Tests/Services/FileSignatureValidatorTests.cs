using FreeSpeakWeb.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

/// <summary>
/// Unit tests for the FileSignatureValidator service.
/// Tests magic byte validation for various image and video formats.
/// </summary>
public class FileSignatureValidatorTests
{
    private readonly FileSignatureValidator _validator;

    public FileSignatureValidatorTests()
    {
        var logger = new Mock<ILogger<FileSignatureValidator>>();
        _validator = new FileSignatureValidator(logger.Object);
    }

    #region JPEG Tests

    [Fact]
    public void ValidateFileSignature_ValidJpegFile_ReturnsValid()
    {
        // Arrange - JPEG magic bytes: FF D8 FF E0
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileName = "test.jpg";

        // Act
        var result = _validator.ValidateFileSignature(jpegBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateFileSignature_JpegWithJpegExtension_ReturnsValid()
    {
        // Arrange - JPEG magic bytes variant: FF D8 FF E1 (EXIF)
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x10, 0x45, 0x78, 0x69, 0x66 };
        var fileName = "photo.jpeg";

        // Act
        var result = _validator.ValidateFileSignature(jpegBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFileSignature_PngContentWithJpgExtension_ReturnsInvalid()
    {
        // Arrange - PNG magic bytes but .jpg extension
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var fileName = "fake.jpg";

        // Act
        var result = _validator.ValidateFileSignature(pngBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.ErrorMessage);
    }

    #endregion

    #region PNG Tests

    [Fact]
    public void ValidateFileSignature_ValidPngFile_ReturnsValid()
    {
        // Arrange - PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var fileName = "image.png";

        // Act
        var result = _validator.ValidateFileSignature(pngBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFileSignature_JpegContentWithPngExtension_ReturnsInvalid()
    {
        // Arrange - JPEG magic bytes but .png extension
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileName = "disguised.png";

        // Act
        var result = _validator.ValidateFileSignature(jpegBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.ErrorMessage);
    }

    #endregion

    #region GIF Tests

    [Fact]
    public void ValidateFileSignature_ValidGif87aFile_ReturnsValid()
    {
        // Arrange - GIF87a magic bytes: 47 49 46 38 37 61
        var gifBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x00, 0x00 };
        var fileName = "animation.gif";

        // Act
        var result = _validator.ValidateFileSignature(gifBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFileSignature_ValidGif89aFile_ReturnsValid()
    {
        // Arrange - GIF89a magic bytes: 47 49 46 38 39 61
        var gifBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00 };
        var fileName = "animated.gif";

        // Act
        var result = _validator.ValidateFileSignature(gifBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region WebP Tests

    [Fact]
    public void ValidateFileSignature_ValidWebpFile_ReturnsValid()
    {
        // Arrange - WebP magic bytes: RIFF....WEBP
        var webpBytes = new byte[] { 
            0x52, 0x49, 0x46, 0x46,  // RIFF
            0x00, 0x00, 0x00, 0x00,  // File size (placeholder)
            0x57, 0x45, 0x42, 0x50   // WEBP
        };
        var fileName = "modern.webp";

        // Act
        var result = _validator.ValidateFileSignature(webpBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Video Tests

    [Fact]
    public void ValidateFileSignature_ValidMp4File_ReturnsValid()
    {
        // Arrange - MP4/ftyp magic bytes (typical MP4 structure with ftyp at offset 4)
        // Real MP4 files start with a box size (4 bytes), then 'ftyp' marker
        var mp4Bytes = new byte[] { 
            0x00, 0x00, 0x00, 0x18,  // Box size (24 bytes)
            0x66, 0x74, 0x79, 0x70,  // ftyp (file type box)
            0x6D, 0x70, 0x34, 0x32,  // mp42 brand
            0x00, 0x00, 0x00, 0x00,  // minor version
            0x6D, 0x70, 0x34, 0x32,  // compatible brand
            0x69, 0x73, 0x6F, 0x6D   // compatible brand
        };
        var fileName = "video.mp4";

        // Act
        var result = _validator.ValidateFileSignature(mp4Bytes, fileName);

        // Assert
        Assert.True(result.IsValid, $"Validation failed: {result.ErrorMessage}");
    }

    [Fact]
    public void ValidateFileSignature_ValidWebmFile_ReturnsValid()
    {
        // Arrange - WebM/EBML magic bytes: 1A 45 DF A3
        var webmBytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x00, 0x00, 0x00, 0x00 };
        var fileName = "clip.webm";

        // Act
        var result = _validator.ValidateFileSignature(webmBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFileSignature_ValidAviFile_ReturnsValid()
    {
        // Arrange - AVI magic bytes: RIFF....AVI
        var aviBytes = new byte[] { 
            0x52, 0x49, 0x46, 0x46,  // RIFF
            0x00, 0x00, 0x00, 0x00,  // File size (placeholder)
            0x41, 0x56, 0x49, 0x20   // AVI 
        };
        var fileName = "movie.avi";

        // Act
        var result = _validator.ValidateFileSignature(aviBytes, fileName);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateFileSignature_EmptyFile_ReturnsInvalid()
    {
        // Arrange
        var emptyBytes = Array.Empty<byte>();
        var fileName = "empty.jpg";

        // Act
        var result = _validator.ValidateFileSignature(emptyBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFileSignature_NoExtension_ReturnsInvalid()
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var fileName = "noextension";

        // Act
        var result = _validator.ValidateFileSignature(jpegBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("extension", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFileSignature_DisallowedExtension_ReturnsInvalid()
    {
        // Arrange - Executable file (not allowed)
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // MZ header
        var fileName = "malware.exe";

        // Act
        var result = _validator.ValidateFileSignature(exeBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFileSignature_UnrecognizedContent_ReturnsInvalid()
    {
        // Arrange - Random bytes that don't match any known signature
        var randomBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var fileName = "unknown.jpg";

        // Act
        var result = _validator.ValidateFileSignature(randomBytes, fileName);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region MIME Type Detection

    [Fact]
    public void DetectMimeType_JpegBytes_ReturnsImageJpeg()
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

        // Act
        var mimeType = _validator.DetectMimeType(jpegBytes);

        // Assert
        Assert.Equal("image/jpeg", mimeType);
    }

    [Fact]
    public void DetectMimeType_PngBytes_ReturnsImagePng()
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

        // Act
        var mimeType = _validator.DetectMimeType(pngBytes);

        // Assert
        Assert.Equal("image/png", mimeType);
    }

    [Fact]
    public void DetectMimeType_UnknownBytes_ReturnsNull()
    {
        // Arrange
        var randomBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var mimeType = _validator.DetectMimeType(randomBytes);

        // Assert
        Assert.Null(mimeType);
    }

    #endregion

    #region Extension Validation

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".jpeg", true)]
    [InlineData(".png", true)]
    [InlineData(".gif", true)]
    [InlineData(".webp", true)]
    [InlineData(".mp4", true)]
    [InlineData(".webm", true)]
    [InlineData(".mov", true)]
    [InlineData(".exe", false)]
    [InlineData(".dll", false)]
    [InlineData(".js", false)]
    [InlineData(".html", false)]
    public void IsAllowedExtension_VariousExtensions_ReturnsExpected(string extension, bool expected)
    {
        // Arrange
        var fileName = $"test{extension}";

        // Act
        var result = _validator.IsAllowedExtension(fileName);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region MIME Type Mapping

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".mp4", "video/mp4")]
    [InlineData(".webm", "video/webm")]
    [InlineData(".mov", "video/quicktime")]
    [InlineData(".unknown", null)]
    public void GetMimeTypeForExtension_VariousExtensions_ReturnsExpected(string extension, string? expected)
    {
        // Act
        var result = _validator.GetMimeTypeForExtension(extension);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion
}
