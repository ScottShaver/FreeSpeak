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
    }
}
