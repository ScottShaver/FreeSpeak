using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;

namespace FreeSpeakWeb.Components.Account;

/// <summary>
/// Helpers for rendering localized sentences that embed links or emphasis.
/// Resource templates use positional placeholders (<c>{0}</c>, <c>{1}</c>) that are replaced
/// with already HTML-safe fragments, so translators can reorder the parts for their language.
/// </summary>
internal static class LocalizedMarkup
{
    /// <summary>
    /// Builds an anchor element with an HTML-encoded href and link text.
    /// </summary>
    /// <param name="href">The link target.</param>
    /// <param name="text">The plain link text (it is HTML-encoded).</param>
    /// <returns>The anchor markup as a string.</returns>
    public static string Link(string href, string text) =>
        $"<a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(text)}</a>";

    /// <summary>
    /// Encodes plain text so it can be safely passed as an argument to <see cref="Format"/>.
    /// </summary>
    /// <param name="text">The plain text.</param>
    /// <returns>The HTML-encoded text.</returns>
    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>
    /// Formats a trusted resource template with HTML-safe arguments and returns it as markup.
    /// The template comes from a .resx file and is not encoded; every argument must already be HTML-safe
    /// (use <see cref="Link"/> or <see cref="Encode"/>).
    /// </summary>
    /// <param name="template">The localized template containing positional placeholders.</param>
    /// <param name="htmlSafeArgs">The HTML-safe replacement fragments.</param>
    /// <returns>The formatted markup.</returns>
    public static MarkupString Format(string template, params string[] htmlSafeArgs) =>
        new(string.Format(CultureInfo.CurrentCulture, template, htmlSafeArgs));
}
