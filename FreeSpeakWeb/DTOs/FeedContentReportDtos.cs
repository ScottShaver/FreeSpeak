using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Describes why a feed report operation failed. Presentation code maps each value to a localized message.
    /// </summary>
    public enum FeedReportError
    {
        /// <summary>No error; the operation succeeded.</summary>
        None = 0,

        /// <summary>The acting user is missing or unknown.</summary>
        NotAuthenticated,

        /// <summary>The content does not exist, was deleted, or the reporter cannot see it.</summary>
        ContentNotFound,

        /// <summary>The reporter authored the content.</summary>
        OwnContent,

        /// <summary>The reporter already reported this content.</summary>
        DuplicateReport,

        /// <summary>The reason is not valid for feed content.</summary>
        InvalidReason,

        /// <summary>The description exceeds the maximum length.</summary>
        DescriptionTooLong,

        /// <summary>The reporter exceeded the hourly report limit.</summary>
        RateLimited,

        /// <summary>The acting user is not a system moderator or administrator.</summary>
        NotAuthorized,

        /// <summary>The report does not exist.</summary>
        ReportNotFound,

        /// <summary>The report was already reviewed.</summary>
        AlreadyReviewed,

        /// <summary>An unexpected failure occurred.</summary>
        Failed
    }

    /// <summary>
    /// The outcome of a feed report operation.
    /// </summary>
    /// <param name="Success">True when the operation succeeded.</param>
    /// <param name="Error">The failure reason, or <see cref="FeedReportError.None"/>.</param>
    /// <param name="ReportId">The affected report ID when available.</param>
    public sealed record FeedReportResult(bool Success, FeedReportError Error, int? ReportId = null)
    {
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="reportId">The affected report ID.</param>
        /// <returns>A successful result.</returns>
        public static FeedReportResult Ok(int reportId) => new(true, FeedReportError.None, reportId);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="error">The failure reason.</param>
        /// <returns>A failed result.</returns>
        public static FeedReportResult Fail(FeedReportError error) => new(false, error);
    }

    /// <summary>
    /// A feed report as shown to system moderators. Never returned to content authors or reporters.
    /// </summary>
    public sealed record FeedContentReportDto(
        int Id,
        int? PostId,
        int? CommentId,
        string ContentAuthorId,
        string ReporterId,
        ReportReason Reason,
        string? Description,
        ReportStatus Status,
        DateTime CreatedAt,
        string? ReviewerId,
        DateTime? ReviewedAt,
        string? ReviewerNotes);

    /// <summary>
    /// A page of feed reports for the moderator queue.
    /// </summary>
    /// <param name="Items">The reports on this page, oldest first.</param>
    /// <param name="TotalCount">The total number of pending reports.</param>
    public sealed record FeedContentReportPage(IReadOnlyList<FeedContentReportDto> Items, int TotalCount);

    /// <summary>
    /// The minimum data about reported content needed to validate a report.
    /// </summary>
    /// <param name="PostId">The post ID (for a comment, the post it belongs to).</param>
    /// <param name="CommentId">The comment ID, or null when the target is a post.</param>
    /// <param name="ContentAuthorId">The author of the reported post or comment.</param>
    /// <param name="PostAuthorId">The author of the post.</param>
    /// <param name="PostFriendId">The friend whose wall the post was written on, if any.</param>
    /// <param name="Audience">The audience of the post.</param>
    public sealed record FeedReportTarget(
        int PostId,
        int? CommentId,
        string ContentAuthorId,
        string PostAuthorId,
        string? PostFriendId,
        AudienceType Audience);
}
