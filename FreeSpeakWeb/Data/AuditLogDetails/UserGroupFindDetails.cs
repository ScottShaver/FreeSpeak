namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user group search audit log entries.
    /// Tracks when users search for groups or browse group listings.
    /// </summary>
    public class UserGroupFindDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the search query used to find groups, if applicable.
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Gets or sets the type of group discovery activity.
        /// Examples: "Search", "Browse", "Recommended", "Trending".
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of group results returned.
        /// </summary>
        public int ResultCount { get; set; }

        /// <summary>
        /// Gets or sets the category filter applied to the search, if any.
        /// </summary>
        public string? CategoryFilter { get; set; }

        /// <summary>
        /// Gets or sets any additional filters applied to the search.
        /// Examples: "Public", "Private", "NearMe".
        /// </summary>
        public string? AdditionalFilters { get; set; }
    }
}
