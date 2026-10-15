using FreeSpeakWeb.Services;
using System.Text;

namespace FreeSpeakWeb.Tests.Services;

public class ImageSignatureHelperTests
{
    [Fact]
    public void IsSupportedImage_Png_ReturnsTrue()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        Assert.True(ImageSignatureHelper.IsSupportedImage(png));
    }

    [Fact]
    public void IsSupportedImage_Jpeg_ReturnsTrue()
    {
        Assert.True(ImageSignatureHelper.IsSupportedImage(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }));
    }

    [Fact]
    public void IsSupportedImage_Gif_ReturnsTrue()
    {
        Assert.True(ImageSignatureHelper.IsSupportedImage(Encoding.ASCII.GetBytes("GIF89a....")));
    }

    [Fact]
    public void IsSupportedImage_WebP_ReturnsTrue()
    {
        Assert.True(ImageSignatureHelper.IsSupportedImage(Encoding.ASCII.GetBytes("RIFF....WEBPVP8 ")));
    }

    [Fact]
    public void IsSupportedImage_TextRenamedAsImage_ReturnsFalse()
    {
        Assert.False(ImageSignatureHelper.IsSupportedImage(Encoding.UTF8.GetBytes("hello, I am not a png")));
    }

    [Fact]
    public void IsSupportedImage_EmptyOrShort_ReturnsFalse()
    {
        Assert.False(ImageSignatureHelper.IsSupportedImage(ReadOnlySpan<byte>.Empty));
        Assert.False(ImageSignatureHelper.IsSupportedImage(new byte[] { 0x89, 0x50 }));
    }
}
