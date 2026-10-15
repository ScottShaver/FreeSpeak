namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Settings for the group moderation log, bound from the <c>GroupModerationLog</c> configuration section.
    /// </summary>
    public class GroupModerationLogSettings
    {
        /// <summary>
        /// The configuration section name.
        /// </summary>
        public const string SectionName = "GroupModerationLog";

        /// <summary>
        /// Gets or sets how many months entries are kept before the cleanup service purges them. Default 6.
        /// </summary>
        public int RetentionMonths { get; set; } = 6;

        /// <summary>
        /// Gets or sets the maximum number of rows a single CSV export returns. Default 10,000.
        /// </summary>
        public int ExportMaxRows { get; set; } = 10000;

        /// <summary>
        /// Gets or sets how many rows the cleanup service deletes per batch. Default 1,000.
        /// </summary>
        public int CleanupBatchSize { get; set; } = 1000;
    }
}
