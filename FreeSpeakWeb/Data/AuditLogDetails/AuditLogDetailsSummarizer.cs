using System.Text.Json;

namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Builds a short, human-readable one-line summary from the raw JSON stored in an audit log entry.
    /// </summary>
    public static class AuditLogDetailsSummarizer
    {
        private const int MaxSummaryLength = 140;

        private static readonly string[] DescriptionProperties = { "description", "message", "reason", "summary" };
        private static readonly string[] HiddenProperties = { "ipAddress", "userAgent", "operationType" };

        /// <summary>
        /// Creates a readable summary of audit log details JSON.
        /// Uses a description-like property when present; otherwise the operation type followed by
        /// the first few scalar values as "name: value" pairs. IP address and user agent are omitted.
        /// </summary>
        /// <param name="json">The raw JSON details string.</param>
        /// <returns>A summary string, or an empty string when nothing meaningful can be extracted.</returns>
        public static string Summarize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return Truncate(json.Trim());
                }

                var root = doc.RootElement;

                foreach (var name in DescriptionProperties)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)
                            && prop.Value.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                        {
                            return Truncate(prop.Value.GetString()!);
                        }
                    }
                }

                var parts = new List<string>();
                if (root.TryGetProperty("operationType", out var op) && op.ValueKind == JsonValueKind.String)
                {
                    parts.Add(op.GetString()!);
                }

                foreach (var prop in root.EnumerateObject())
                {
                    if (parts.Count >= 4)
                    {
                        break;
                    }

                    if (HiddenProperties.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                    {
                        continue;
                    }

                    parts.Add($"{prop.Name}: {prop.Value}");
                }

                return Truncate(string.Join(", ", parts));
            }
            catch (JsonException)
            {
                return Truncate(json.Trim());
            }
        }

        /// <summary>
        /// Truncates text to the maximum summary length, appending an ellipsis when shortened.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <returns>The possibly truncated text.</returns>
        private static string Truncate(string text)
        {
            return text.Length <= MaxSummaryLength ? text : text[..(MaxSummaryLength - 1)] + "…";
        }
    }
}
