using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Filters and ordering for the system moderator group report queue.
    /// </summary>
    /// <param name="GroupId">Restrict to one group, or null for all groups.</param>
    /// <param name="Status">Restrict to one pending status (not reviewed or under review), or null for both.</param>
    /// <param name="Reason">Restrict to one report reason, or null.</param>
    /// <param name="ContentType">Restrict to posts or comments, or null.</param>
    /// <param name="StaleOnly">When true, only reports older than the stale threshold are returned.</param>
    /// <param name="NewestFirst">When true the queue is ordered newest first; the default is oldest first.</param>
    public sealed record GroupReportQueueFilter(
        int? GroupId = null,
        ReportStatus? Status = null,
        ReportReason? Reason = null,
        FeedReportContentType? ContentType = null,
        bool StaleOnly = false,
        bool NewestFirst = false);

    /// <summary>
    /// A raw group report queue row read from the database, before display formatting.
    /// </summary>
    public sealed record GroupReportQueueRow(
        int Id,
        int GroupId,
        string GroupName,
        int? PostId,
        int? CommentId,
        ReportReason Reason,
        ReportStatus Status,
        DateTime CreatedAt,
        string? ContentAuthorId,
        string? AuthorName,
        string ReporterId,
        string? ReporterName,
        string? Content,
        int OtherPendingReports);

    /// <summary>
    /// One row of the system moderator group report queue.
    /// </summary>
    public sealed record GroupReportQueueItem(
        int Id,
        int GroupId,
        string GroupName,
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
    /// A page of the system moderator group report queue.
    /// </summary>
    /// <param name="Items">The rows on this page.</param>
    /// <param name="TotalCount">The number of reports that match the filter.</param>
    public sealed record GroupReportQueuePage(IReadOnlyList<GroupReportQueueItem> Items, int TotalCount);

    /// <summary>
    /// A group that currently has pending reports, used to populate the group filter.
    /// </summary>
    /// <param name="Id">The group ID.</param>
    /// <param name="Name">The group name.</param>
    public sealed record GroupReportGroupOption(int Id, string Name);

    /// <summary>
    /// The reported group content as it currently exists.
    /// </summary>
    public sealed record GroupReportContentSnapshot(
        int GroupId,
        string GroupName,
        int PostId,
        int? CommentId,
        string AuthorId,
        string? AuthorName,
        string Content,
        DateTime CreatedAt);

    /// <summary>
    /// A pending report on group content, including what is needed to restore or notify it.
    /// </summary>
    public sealed record GroupPendingReportRef(
        int Id,
        string ReporterId,
        ReportStatus Status,
        string? ReviewerId,
        DateTime? ReviewedAt,
        string? ReviewerNotes,
        string? ContentAuthorId,
        int? PostId,
        int? CommentId);

    /// <summary>
    /// The full detail of one group report for the system moderator detail view.
    /// </summary>
    public sealed record GroupReportDetail(
        int Id,
        int GroupId,
        string GroupName,
        FeedReportContentType ContentType,
        int? PostId,
        int? CommentId,
        ReportReason Reason,
        string? ViolatedRuleTitle,
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
