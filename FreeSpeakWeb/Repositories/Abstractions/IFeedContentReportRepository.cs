using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Data access for <see cref="FeedContentReport"/> and the feed content it targets.
    /// </summary>
    public interface IFeedContentReportRepository
    {
        /// <summary>
        /// Gets the data needed to validate a report against a feed post.
        /// </summary>
        /// <param name="postId">The post ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The target, or null when the post does not exist.</returns>
        Task<FeedReportTarget?> GetPostTargetAsync(int postId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the data needed to validate a report against a feed comment.
        /// </summary>
        /// <param name="commentId">The comment ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The target, or null when the comment does not exist.</returns>
        Task<FeedReportTarget?> GetCommentTargetAsync(int commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether a user already reported the given post or comment.
        /// </summary>
        /// <param name="reporterId">The reporter's user ID.</param>
        /// <param name="postId">The post ID, or null.</param>
        /// <param name="commentId">The comment ID, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when a report exists.</returns>
        Task<bool> HasReportedAsync(string reporterId, int? postId, int? commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the reports a user submitted at or after the given UTC time.
        /// </summary>
        /// <param name="reporterId">The reporter's user ID.</param>
        /// <param name="sinceUtc">The UTC lower bound.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of reports.</returns>
        Task<int> CountReportsSinceAsync(string reporterId, DateTime sinceUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a report. A unique-index violation is reported as a duplicate rather than thrown.
        /// </summary>
        /// <param name="report">The report to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>False when the report duplicates an existing one; otherwise true.</returns>
        Task<bool> AddAsync(FeedContentReport report, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a report by ID.
        /// </summary>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The report, or null.</returns>
        Task<FeedContentReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets pending (not reviewed or under review) reports, oldest first.
        /// </summary>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page of reports and the total pending count.</returns>
        Task<(List<FeedContentReport> Items, int TotalCount)> GetPendingPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of pending (not reviewed or under review) reports.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The pending count.</returns>
        Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a reviewer decision on a report.
        /// </summary>
        /// <param name="reportId">The report ID.</param>
        /// <param name="status">The new status.</param>
        /// <param name="reviewerId">The reviewer's user ID.</param>
        /// <param name="reviewedAtUtc">The server-set review time.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated report, or null when it does not exist.</returns>
        Task<FeedContentReport?> UpdateReviewAsync(int reportId, ReportStatus status, string reviewerId, DateTime reviewedAtUtc, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one page of the moderator queue (pending reports only) with display data, using the filter and ordering given.
        /// </summary>
        /// <param name="filter">The filters and ordering.</param>
        /// <param name="staleBeforeUtc">Reports created before this UTC time are stale.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rows and the total number of matching reports.</returns>
        Task<(List<FeedReportQueueRow> Items, int TotalCount)> GetQueueAsync(FeedReportQueueFilter filter, DateTime staleBeforeUtc, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the counts shown on the dashboard overview.
        /// </summary>
        /// <param name="staleBeforeUtc">Pending reports created before this UTC time are stale.</param>
        /// <param name="closedSinceUtc">The UTC lower bound for the closed-reports count.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The not reviewed, under review, closed-since and stale counts.</returns>
        Task<(int NotReviewed, int UnderReview, int ClosedSince, int Stale)> GetOverviewCountsAsync(DateTime staleBeforeUtc, DateTime closedSinceUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the reported content as it currently exists.
        /// </summary>
        /// <param name="postId">The post ID, or null.</param>
        /// <param name="commentId">The comment ID, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The content snapshot, or null when it no longer exists.</returns>
        Task<FeedReportContentSnapshot?> GetContentSnapshotAsync(int? postId, int? commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets every report on the same content as the given report, excluding that report, newest last.
        /// </summary>
        /// <param name="postId">The post ID, or null.</param>
        /// <param name="commentId">The comment ID, or null.</param>
        /// <param name="excludeReportId">The report to leave out.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The other reports with reporter display names.</returns>
        Task<List<FeedReportRelatedItem>> GetOtherReportsAsync(int? postId, int? commentId, int excludeReportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts all reports ever filed against a content author's content, excluding one report.
        /// </summary>
        /// <param name="authorId">The content author's user ID.</param>
        /// <param name="excludeReportId">The report to leave out.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of other reports about the author.</returns>
        Task<int> CountReportsAgainstAuthorAsync(string authorId, int excludeReportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the IDs of the reporters of every pending report on the same content, including the given report.
        /// </summary>
        /// <param name="postId">The post ID, or null.</param>
        /// <param name="commentId">The comment ID, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Report ID and reporter ID pairs.</returns>
        Task<List<(int ReportId, string ReporterId)>> GetPendingReportersAsync(int? postId, int? commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically moves a report to a new status only when it is currently in one of the allowed statuses.
        /// </summary>
        /// <param name="reportId">The report ID.</param>
        /// <param name="allowedFrom">The statuses the report may currently have.</param>
        /// <param name="status">The new status.</param>
        /// <param name="reviewerId">The reviewer's user ID, or null to clear it.</param>
        /// <param name="reviewedAtUtc">The server-set review time, or null to clear it.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated report, or null when it does not exist or was no longer in an allowed status.</returns>
        Task<FeedContentReport?> TryTransitionAsync(int reportId, IReadOnlyCollection<ReportStatus> allowedFrom, ReportStatus status, string? reviewerId, DateTime? reviewedAtUtc, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes every other pending report on the same content with the given outcome.
        /// </summary>
        /// <param name="postId">The post ID, or null.</param>
        /// <param name="commentId">The comment ID, or null.</param>
        /// <param name="excludeReportId">The report that was already closed.</param>
        /// <param name="status">The outcome to apply.</param>
        /// <param name="reviewerId">The reviewer's user ID.</param>
        /// <param name="reviewedAtUtc">The server-set review time.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reports that were closed.</returns>
        Task<List<FeedContentReport>> CloseOtherPendingAsync(int? postId, int? commentId, int excludeReportId, ReportStatus status, string reviewerId, DateTime reviewedAtUtc, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a user's display name (first and last name).
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The display name, or null when the user does not exist.</returns>
        Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    }
}
