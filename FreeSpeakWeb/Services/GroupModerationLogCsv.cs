using System.Text;
using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Builds safe CSV text for moderation log exports.
    /// </summary>
    public static class GroupModerationLogCsv
    {
        /// <summary>
        /// The CSV header row columns, in order.
        /// </summary>
        public static readonly string[] Columns =
        {
            "Timestamp (UTC)", "Actor", "Action", "Target Type", "Target", "Target User", "Summary", "Reason"
        };

        /// <summary>
        /// Builds a CSV document (CRLF line endings) from the given entries.
        /// </summary>
        /// <param name="entries">The entries to write.</param>
        /// <returns>The CSV text including the header row.</returns>
        public static string Build(IEnumerable<GroupModerationLogEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Columns.Select(Escape))).Append("\r\n");

            foreach (var e in entries)
            {
                var cells = new[]
                {
                    e.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                    e.ActorDisplayName,
                    e.Action.ToString(),
                    e.TargetType.ToString(),
                    e.TargetId ?? string.Empty,
                    e.TargetUserDisplayName ?? string.Empty,
                    e.Summary ?? string.Empty,
                    e.Reason ?? string.Empty
                };
                sb.Append(string.Join(",", cells.Select(Escape))).Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes one cell: neutralizes spreadsheet formulas with a leading single quote and
        /// quotes the value when it contains commas, quotes or line breaks.
        /// </summary>
        /// <param name="value">The raw cell text.</param>
        /// <returns>The safe cell text.</returns>
        public static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                value = "'" + value;
            }

            if (value.AsSpan().IndexOfAny(",\"\r\n") >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
