namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Settings for feed content reporting, bound from the <c>FeedReporting</c> configuration section.
    /// </summary>
    public class FeedReportSettings
    {
        /// <summary>
        /// The configuration section name.
        /// </summary>
        public const string SectionName = "FeedReporting";

        /// <summary>
        /// Gets or sets the maximum number of reports a single user may submit per hour. Default 5.
        /// </summary>
        public int MaxReportsPerUserPerHour { get; set; } = 5;
    }
}
