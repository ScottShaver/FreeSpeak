namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user feed post audit log entries.
    /// Tracks when users create personal or friends-only posts.
    /// </summary>
    public class UserPostDetails
    {
        /// <summary>
        /// Gets or sets the unique identifier of the created post.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the visibility level of the post.
        /// Examples: "Public", "FriendsOnly", "Private".
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Gets or sets a brief summary or excerpt of the post content.
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the post contains media attachments.
        /// </summary>
        public bool HasMedia { get; set; }

        /// <summary>
        /// Gets or sets the type of media attached to the post, if any.
        /// Examples: "Image", "Video", "Link".
        /// </summary>
        public string? MediaType { get; set; }
    }
}
