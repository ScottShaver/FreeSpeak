namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user reaction audit log entries.
    /// Tracks when users add, change, or remove reactions on posts and comments.
    /// </summary>
    public class UserReactionDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the post being reacted to.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the comment being reacted to.
        /// Null if the reaction is on a post rather than a comment.
        /// </summary>
        public int? CommentId { get; set; }

        /// <summary>
        /// Gets or sets the type of reaction.
        /// Examples: "Like", "Love", "Haha", "Wow", "Sad", "Angry".
        /// </summary>
        public string? ReactionType { get; set; }

        /// <summary>
        /// Gets or sets whether this reaction is on a comment (true) or post (false).
        /// </summary>
        public bool IsCommentReaction { get; set; }
    }
}
