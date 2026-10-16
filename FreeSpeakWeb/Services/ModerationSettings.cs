namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Settings for the system moderator dashboard, bound from the <c>Moderation</c> configuration section.
    /// </summary>
    public class ModerationSettings
    {
        /// <summary>
        /// The configuration section name.
        /// </summary>
        public const string SectionName = "Moderation";

        /// <summary>
        /// The configuration key of <see cref="StaleReportDays"/>.
        /// </summary>
        public const string StaleReportDaysKey = "Moderation:StaleReportDays";

        /// <summary>
        /// The default number of days after which a pending report is considered stale.
        /// </summary>
        public const int DefaultStaleReportDays = 3;

        /// <summary>
        /// Gets or sets the number of days a report may stay pending before the dashboard flags it as stale. Default 3.
        /// </summary>
        public int StaleReportDays { get; set; } = DefaultStaleReportDays;

        /// <summary>
        /// The configuration key of <see cref="MaxModeratorSuspensionDays"/>.
        /// </summary>
        public const string MaxModeratorSuspensionDaysKey = "Moderation:MaxModeratorSuspensionDays";

        /// <summary>
        /// The default maximum suspension length, in days, a system moderator may issue.
        /// </summary>
        public const int DefaultMaxModeratorSuspensionDays = 7;

        /// <summary>
        /// Gets or sets the longest suspension, in days, a system moderator may issue. Only administrators can
        /// suspend for longer or permanently. Default 7.
        /// </summary>
        public int MaxModeratorSuspensionDays { get; set; } = DefaultMaxModeratorSuspensionDays;
    }
}
