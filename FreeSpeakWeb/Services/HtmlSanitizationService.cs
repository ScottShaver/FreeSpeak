using Ganss.Xss;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for sanitizing user-provided HTML content to prevent XSS attacks
/// </summary>
public class HtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizationService()
    {
        _sanitizer = new HtmlSanitizer();
        
        // Configure allowed tags - only allow safe formatting
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.Add("br");
        _sanitizer.AllowedTags.Add("p");
        _sanitizer.AllowedTags.Add("b");
        _sanitizer.AllowedTags.Add("i");
        _sanitizer.AllowedTags.Add("u");
        _sanitizer.AllowedTags.Add("em");
        _sanitizer.AllowedTags.Add("strong");
        
        // Don't allow any attributes to prevent onclick, onload, etc.
        _sanitizer.AllowedAttributes.Clear();
        
        // Don't allow any CSS
        _sanitizer.AllowedCssProperties.Clear();
        
        // Don't allow any schemes (prevents javascript:, data:, etc.)
        _sanitizer.AllowedSchemes.Clear();
    }

    /// <summary>
    /// Sanitizes user content and formats it for display with line breaks
    /// </summary>
    public string SanitizeAndFormatContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // First, HTML encode the entire content to prevent XSS
        var encoded = System.Net.WebUtility.HtmlEncode(content);
        
        // Then replace newlines with <br> tags
        // Handle both \r\n (Windows) and \n (Unix) line endings
        var formatted = encoded.Replace("\r\n", "<br>").Replace("\n", "<br>");
        
        // Sanitize the result (should only contain text and <br> tags now)
        return _sanitizer.Sanitize(formatted);
    }

    /// <summary>
    /// Sanitizes HTML content (for when HTML tags might be intentionally allowed in future)
    /// </summary>
    public string Sanitize(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        return _sanitizer.Sanitize(html);
    }
}
