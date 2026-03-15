namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user comment audit log entries.
    /// Tracks when users create, edit, or delete comments on posts.
    /// </summary>
    public class UserCommentDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the comment.
        /// </summary>
        public int CommentId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the post the comment belongs to.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the parent comment ID if this is a reply.
        /// Null for top-level comments.
        /// </summary>
        public int? ParentCommentId { get; set; }
    }
}
