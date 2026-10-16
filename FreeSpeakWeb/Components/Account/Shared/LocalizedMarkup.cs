using System.Net;
using Microsoft.AspNetCore.Components;

namespace FreeSpeakWeb.Components.Account.Shared;

/// <summary>
/// Helpers for localized sentences that embed links or other markup. The sentence template comes from a
/// trusted resource file; every inserted argument is HTML-encoded unless it is produced by <see cref="Link"/>
/// or <see cref="Code"/>.
/// </summary>
public static class LocalizedMarkup
{
    /// <summary>
    /// Builds an anchor element with an HTML-encoded href and link text.
    /// </summary>
    /// <param name="href">The link target.</param>
    /// <param name="text">The (localized) link text.</param>
    /// <returns>The anchor markup as a string.</returns>
    public static string Link(string href, string text) =>
        $"<a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(text)}</a>";

    /// <summary>
    /// Builds a <c>kbd</c> element with HTML-encoded content.
    /// </summary>
    /// <param name="text">The text to show.</param>
    /// <returns>The kbd markup as a string.</returns>
    public static string Code(string? text) => $"<kbd>{WebUtility.HtmlEncode(text ?? string.Empty)}</kbd>";

    /// <summary>
    /// Formats a localized template with pre-built markup arguments.
    /// </summary>
    /// <param name="template">The localized template containing <c>{0}</c>-style placeholders.</param>
    /// <param name="markupArgs">Markup produced by <see cref="Link"/> or <see cref="Code"/>.</param>
    /// <returns>The formatted markup.</returns>
    public static MarkupString Compose(string template, params string[] markupArgs) =>
        new(string.Format(template, markupArgs));
}
