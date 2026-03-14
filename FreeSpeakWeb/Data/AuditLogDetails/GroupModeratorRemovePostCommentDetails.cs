namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group moderator remove post comment audit log entries.
    /// Tracks when moderators remove comments from group posts.
    /// </summary>
    public class GroupModeratorRemovePostCommentDetails
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
        /// Gets or sets the unique identifier of the post containing the comment.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the removed comment.
        /// </summary>
        public int CommentId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the comment author.
        /// </summary>
        public string CommentAuthorId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the comment author.
        /// </summary>
        public string? CommentAuthorDisplayName { get; set; }

        /// <summary>
        /// Gets or sets a brief summary of the comment content.
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// Gets or sets the reason for removing the comment.
        /// </summary>
        public string? Reason { get; set; }
    }
}
