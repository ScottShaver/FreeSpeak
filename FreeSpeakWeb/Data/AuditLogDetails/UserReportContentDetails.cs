namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for audit log entries recorded when a user reports a feed post or comment.
    /// </summary>
    public class UserReportContentDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the ID of the created report.
        /// </summary>
        public int ReportId { get; set; }

        /// <summary>
        /// Gets or sets the reported post ID, when the report targets a post.
        /// </summary>
        public int? PostId { get; set; }

        /// <summary>
        /// Gets or sets the reported comment ID, when the report targets a comment.
        /// </summary>
        public int? CommentId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the author of the reported content.
        /// </summary>
        public string ContentAuthorId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the selected report reason.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
