using System.Net;
using System.Text.RegularExpressions;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Helpers for turning stored HTML content into short plain-text excerpts.
    /// </summary>
    public static partial class PlainTextExcerpt
    {
        [GeneratedRegex("<[^>]*>")]
        private static partial Regex HtmlTags();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();

        /// <summary>
        /// Strips HTML tags, decodes entities, collapses whitespace and truncates to a maximum length.
        /// </summary>
        /// <param name="html">The stored content, possibly containing HTML.</param>
        /// <param name="maxLength">The maximum length of the result, including the ellipsis.</param>
        /// <returns>The plain-text excerpt, or an empty string for empty input.</returns>
        public static string Create(string? html, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(html) || maxLength <= 0)
            {
                return string.Empty;
            }

            var text = WebUtility.HtmlDecode(HtmlTags().Replace(html, " "));
            text = Whitespace().Replace(text, " ").Trim();
            if (text.Length <= maxLength)
            {
                return text;
            }

            return maxLength <= 1 ? text[..maxLength] : text[..(maxLength - 1)].TrimEnd() + "…";
        }
    }
}
