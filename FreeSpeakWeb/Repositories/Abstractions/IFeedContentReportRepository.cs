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
    }
}
