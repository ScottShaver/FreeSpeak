namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user friend search audit log entries.
    /// Tracks when users search for friends or view friend suggestions.
    /// </summary>
    public class UserFriendsFindDetails
    {
        /// <summary>
        /// Gets or sets the search query used to find friends, if applicable.
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Gets or sets the type of friend discovery activity.
        /// Examples: "Search", "Suggestions", "MutualFriends", "NearbyUsers".
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of results returned from the search or suggestion.
        /// </summary>
        public int ResultCount { get; set; }

        /// <summary>
        /// Gets or sets any filters applied to the search.
        /// Examples: "SameCity", "SameSchool", "SameInterests".
        /// </summary>
        public string? Filters { get; set; }
    }
}
