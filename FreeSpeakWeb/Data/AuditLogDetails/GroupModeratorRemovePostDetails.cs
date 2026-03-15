namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group moderator remove post audit log entries.
    /// Tracks when moderators remove existing posts from groups.
    /// </summary>
    public class GroupModeratorRemovePostDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the removed post.
        /// </summary>
        public int PostId { get; set; }

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
        /// Gets or sets the reason for removing the post.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the date when the post was originally created.
        /// </summary>
        public DateTime? PostCreatedAt { get; set; }
    }
}
