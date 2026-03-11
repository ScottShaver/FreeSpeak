using FreeSpeakWeb.Services;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

public class HtmlSanitizationServiceTests
{
    private readonly HtmlSanitizationService _service;

    public HtmlSanitizationServiceTests()
    {
        _service = new HtmlSanitizationService();
    }

    [Fact]
    public void SanitizeAndFormatContent_WithScriptTag_EncodesAndPreventsExecution()
    {
        // Arrange
        var maliciousContent = "<script>alert('XSS')</script>Hello World";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Script tags are HTML encoded, not executed
        Assert.DoesNotContain("<script>", result); // Raw tag removed
        Assert.Contains("&lt;script&gt;", result); // Encoded tag present (safe)
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithOnclickAttribute_EncodesAttribute()
    {
        // Arrange
        var maliciousContent = "<div onclick=\"alert('XSS')\">Click me</div>";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Attributes are encoded, not executed
        Assert.DoesNotContain("<div onclick=", result); // Raw attribute removed
        Assert.Contains("&lt;div", result); // Tags encoded (safe)
        Assert.Contains("Click me", result); // Text preserved
    }

    [Fact]
    public void SanitizeAndFormatContent_WithJavascriptUrl_EncodesUrl()
    {
        // Arrange
        var maliciousContent = "<a href=\"javascript:alert('XSS')\">Link</a>";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - URL is encoded, not executed
        Assert.DoesNotContain("<a href=", result); // Raw tag removed
        Assert.Contains("&lt;a", result); // Tag encoded (safe)
        Assert.Contains("Link", result); // Text preserved
    }

    [Fact]
    public void SanitizeAndFormatContent_WithDataUrl_EncodesUrl()
    {
        // Arrange
        var maliciousContent = "<img src=\"data:text/html,<script>alert('XSS')</script>\">";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Data URL is encoded, not executed
        Assert.DoesNotContain("<img src=\"data:", result); // Raw data: URL removed
        Assert.Contains("&lt;img", result); // Tag encoded (safe)
    }

    [Fact]
    public void SanitizeAndFormatContent_WithIframe_EncodesIframe()
    {
        // Arrange
        var maliciousContent = "<iframe src=\"evil.com\"></iframe>Normal text";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Iframe is encoded, not rendered
        Assert.DoesNotContain("<iframe", result); // Raw tag removed
        Assert.Contains("&lt;iframe", result); // Encoded tag present (safe)
        Assert.Contains("Normal text", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithLineBreaks_ConvertsToBreakTags()
    {
        // Arrange
        var content = "Line 1\nLine 2\r\nLine 3";

        // Act
        var result = _service.SanitizeAndFormatContent(content);

        // Assert
        Assert.Contains("Line 1<br>Line 2", result);
        Assert.Contains("Line 2<br>Line 3", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithNormalText_PreservesText()
    {
        // Arrange
        var content = "This is normal text with no HTML";

        // Act
        var result = _service.SanitizeAndFormatContent(content);

        // Assert
        Assert.Equal("This is normal text with no HTML", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithEmptyString_ReturnsEmpty()
    {
        // Act
        var result = _service.SanitizeAndFormatContent("");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithNull_ReturnsEmpty()
    {
        // Act
        var result = _service.SanitizeAndFormatContent(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithStyleTag_EncodesStyle()
    {
        // Arrange
        var maliciousContent = "<style>body { display: none; }</style>Text";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Style is encoded, not applied
        Assert.DoesNotContain("<style>", result); // Raw tag removed
        Assert.Contains("&lt;style&gt;", result); // Encoded tag present (safe)
        Assert.Contains("Text", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithSvgXss_EncodesSvg()
    {
        // Arrange
        var maliciousContent = "<svg onload=\"alert('XSS')\"><circle/></svg>";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - SVG is encoded, not rendered
        Assert.DoesNotContain("<svg", result); // Raw tag removed
        Assert.Contains("&lt;svg", result); // Encoded tag present (safe)
    }

    [Fact]
    public void SanitizeAndFormatContent_WithObjectEmbed_EncodesObject()
    {
        // Arrange
        var maliciousContent = "<object data=\"evil.swf\"></object>Text";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - Object is encoded, not embedded
        Assert.DoesNotContain("<object", result); // Raw tag removed
        Assert.Contains("&lt;object", result); // Encoded tag present (safe)
        Assert.Contains("Text", result);
    }

    [Fact]
    public void SanitizeAndFormatContent_WithMultipleAttacks_EncodesAll()
    {
        // Arrange
        var maliciousContent = @"
            <script>alert('XSS1')</script>
            <img src=x onerror=""alert('XSS2')"">
            <div onclick=""alert('XSS3')"">Click</div>
            <iframe src=""evil.com""></iframe>
            Normal content here
        ";

        // Act
        var result = _service.SanitizeAndFormatContent(maliciousContent);

        // Assert - All attacks encoded, not executed
        Assert.DoesNotContain("<script>", result); // Raw tags removed
        Assert.DoesNotContain("<img src=", result);
        Assert.DoesNotContain("<div onclick=", result);
        Assert.DoesNotContain("<iframe", result);

        // Encoded versions present (safe)
        Assert.Contains("&lt;script&gt;", result);
        Assert.Contains("&lt;img", result);
        Assert.Contains("&lt;div", result);
        Assert.Contains("&lt;iframe", result);

        // Normal content preserved
        Assert.Contains("Normal content here", result);
    }
}
