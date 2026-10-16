using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// The kind of feed content a report targets.
    /// </summary>
    public enum FeedReportContentType
    {
        /// <summary>A feed post.</summary>
        Post = 0,

        /// <summary>A feed comment or reply.</summary>
        Comment = 1
    }

    /// <summary>
    /// Filters and ordering for the system moderator feed report queue.
    /// </summary>
    /// <param name="Status">Restrict to one pending status (not reviewed or under review), or null for both.</param>
    /// <param name="Reason">Restrict to one report reason, or null.</param>
    /// <param name="ContentType">Restrict to posts or comments, or null.</param>
    /// <param name="FromUtc">Inclusive lower bound for the report creation time (UTC), or null.</param>
    /// <param name="ToUtc">Exclusive upper bound for the report creation time (UTC), or null.</param>
    /// <param name="StaleOnly">When true, only reports older than the stale threshold are returned.</param>
    /// <param name="NewestFirst">When true the queue is ordered newest first; the default is oldest first.</param>
    public sealed record FeedReportQueueFilter(
        ReportStatus? Status = null,
        ReportReason? Reason = null,
        FeedReportContentType? ContentType = null,
        DateTime? FromUtc = null,
        DateTime? ToUtc = null,
        bool StaleOnly = false,
        bool NewestFirst = false);

    /// <summary>
    /// A raw queue row read from the database, before display formatting.
    /// </summary>
    public sealed record FeedReportQueueRow(
        int Id,
        int? PostId,
        int? CommentId,
        ReportReason Reason,
        ReportStatus Status,
        DateTime CreatedAt,
        string ContentAuthorId,
        string? AuthorName,
        string ReporterId,
        string? ReporterName,
        string? Content,
        int OtherPendingReports);

    /// <summary>
    /// One row of the moderator feed report queue.
    /// </summary>
    public sealed record FeedReportQueueItem(
        int Id,
        FeedReportContentType ContentType,
        int? PostId,
        int? CommentId,
        ReportReason Reason,
        ReportStatus Status,
        DateTime CreatedAt,
        string ContentExcerpt,
        string AuthorId,
        string AuthorDisplayName,
        string ReporterId,
        string ReporterDisplayName,
        int OtherPendingReports,
        bool IsStale);

    /// <summary>
    /// A page of the moderator feed report queue.
    /// </summary>
    /// <param name="Items">The rows on this page.</param>
    /// <param name="TotalCount">The number of reports that match the filter.</param>
    public sealed record FeedReportQueuePage(IReadOnlyList<FeedReportQueueItem> Items, int TotalCount);

    /// <summary>
    /// Counts shown on the moderator dashboard overview.
    /// </summary>
    /// <param name="Pending">Reports that have not been reviewed yet.</param>
    /// <param name="UnderReview">Reports a moderator marked as under review.</param>
    /// <param name="ClosedLast7Days">Reports closed in the last 7 days.</param>
    /// <param name="Stale">Pending reports older than the stale threshold.</param>
    /// <param name="StaleDays">The configured stale threshold in days.</param>
    public sealed record ModerationOverview(int Pending, int UnderReview, int ClosedLast7Days, int Stale, int StaleDays);

    /// <summary>
    /// The reported content as it currently exists.
    /// </summary>
    /// <param name="PostId">The post ID (for a comment, its parent post).</param>
    /// <param name="CommentId">The comment ID, or null for a post.</param>
    /// <param name="AuthorId">The content author's user ID.</param>
    /// <param name="AuthorName">The content author's display name.</param>
    /// <param name="Content">The stored content (HTML that must be sanitized before display).</param>
    /// <param name="CreatedAt">When the content was created (UTC).</param>
    public sealed record FeedReportContentSnapshot(
        int PostId,
        int? CommentId,
        string AuthorId,
        string? AuthorName,
        string Content,
        DateTime CreatedAt);

    /// <summary>
    /// Another report on the same content, shown in the detail view.
    /// </summary>
    public sealed record FeedReportRelatedItem(
        int Id,
        ReportReason Reason,
        ReportStatus Status,
        DateTime CreatedAt,
        string ReporterId,
        string ReporterDisplayName,
        string? Description);

    /// <summary>
    /// The full detail of one feed report for the moderator detail view.
    /// </summary>
    public sealed record FeedReportDetail(
        int Id,
        FeedReportContentType ContentType,
        int? PostId,
        int? CommentId,
        ReportReason Reason,
        string? Description,
        ReportStatus Status,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        string? ReviewerNotes,
        string ReporterId,
        string ReporterDisplayName,
        string AuthorId,
        string AuthorDisplayName,
        string ContentHtml,
        DateTime ContentCreatedAt,
        IReadOnlyList<FeedReportRelatedItem> OtherReports,
        int AuthorOtherReportCount,
        bool IsStale,
        bool CanAct);
}
