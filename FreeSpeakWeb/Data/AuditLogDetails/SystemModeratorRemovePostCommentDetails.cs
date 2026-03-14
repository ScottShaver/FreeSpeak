namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for system moderator remove post comment audit log entries.
    /// Tracks when system-level moderators remove comments for policy violations.
    /// </summary>
    public class SystemModeratorRemovePostCommentDetails
    {
        /// <summary>
        /// Gets or sets the unique identifier of the post containing the comment.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the type of post containing the comment.
        /// Examples: "FeedPost", "GroupPost".
        /// </summary>
        public string PostType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the removed comment.
        /// </summary>
        public int CommentId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the group, if the comment is on a group post.
        /// </summary>
        public int? GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group, if the comment is on a group post.
        /// </summary>
        public string? GroupName { get; set; }

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
        /// Gets or sets the policy violation reason for removal.
        /// </summary>
        public string? ViolationReason { get; set; }

        /// <summary>
        /// Gets or sets any additional notes from the moderator.
        /// </summary>
        public string? ModeratorNotes { get; set; }
    }
}
