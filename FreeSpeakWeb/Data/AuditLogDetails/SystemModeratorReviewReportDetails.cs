namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for audit log entries recorded when a system moderator or administrator
    /// decides on a feed content report.
    /// </summary>
    public class SystemModeratorReviewReportDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the ID of the reviewed report.
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
        /// Gets or sets the status the report was moved to (Dismissed, Resolved or ActionTaken).
        /// </summary>
        public string Decision { get; set; } = string.Empty;
    }
}
