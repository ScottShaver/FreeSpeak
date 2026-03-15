namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for system moderator remove post audit log entries.
    /// Tracks when system-level moderators remove posts for policy violations.
    /// </summary>
    public class SystemModeratorRemovePostDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the removed post.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the type of post being removed.
        /// Examples: "FeedPost", "GroupPost".
        /// </summary>
        public string PostType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the group, if the post is a group post.
        /// </summary>
        public int? GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group, if the post is a group post.
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the post author.
        /// </summary>
        public string PostAuthorId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the post author.
        /// </summary>
        public string? PostAuthorDisplayName { get; set; }

        /// <summary>
        /// Gets or sets a brief summary of the post content.
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// Gets or sets the policy violation reason for removal.
        /// </summary>
        public string? ViolationReason { get; set; }

        /// <summary>
        /// Gets or sets any additional notes from the moderator.
        /// </summary>
        public string? ModeratorNotes { get; set; }
    }
}
