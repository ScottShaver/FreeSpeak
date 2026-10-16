using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Data access for the system moderator view of group content reports across all groups.
    /// Reports whose content has been deleted (both post and comment IDs cleared) are excluded from every query.
    /// </summary>
    public interface IGroupReportModerationRepository
    {
        /// <summary>
        /// Gets a report with its group and violated rule, or null when it does not exist.
        /// </summary>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The report, or null.</returns>
        Task<GroupContentReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one page of the pending report queue across all groups.
        /// </summary>
        /// <param name="filter">The filters and ordering.</param>
        /// <param name="staleBeforeUtc">Reports created before this instant are stale.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rows and the total number of matching reports.</returns>
        Task<(List<GroupReportQueueRow> Items, int TotalCount)> GetQueueAsync(GroupReportQueueFilter filter, DateTime staleBeforeUtc, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the overview counts for group reports.
        /// </summary>
        /// <param name="staleBeforeUtc">Pending reports created before this instant are stale.</param>
        /// <param name="closedSinceUtc">Reports closed on or after this instant are counted as recently closed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The not reviewed, under review, recently closed and stale counts.</returns>
        Task<(int NotReviewed, int UnderReview, int ClosedSince, int Stale)> GetOverviewCountsAsync(DateTime staleBeforeUtc, DateTime closedSinceUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the groups that currently have pending reports, ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The groups.</returns>
        Task<List<GroupReportGroupOption>> GetGroupsWithPendingReportsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the reported content as it currently exists.
        /// </summary>
        /// <param name="postId">The reported post, or null.</param>
        /// <param name="commentId">The reported comment, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The content, or null when it no longer exists.</returns>
        Task<GroupReportContentSnapshot?> GetContentSnapshotAsync(int? postId, int? commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets every other report (any status) on the same content, oldest first.
        /// </summary>
        /// <param name="postId">The reported post, or null.</param>
        /// <param name="commentId">The reported comment, or null.</param>
        /// <param name="excludeReportId">The report to leave out.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The related reports.</returns>
        Task<List<FeedReportRelatedItem>> GetOtherReportsAsync(int? postId, int? commentId, int excludeReportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the other group reports filed against content written by the same author.
        /// </summary>
        /// <param name="authorId">The content author's user ID.</param>
        /// <param name="excludeReportId">The report to leave out.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of other reports.</returns>
        Task<int> CountReportsAgainstAuthorAsync(string authorId, int excludeReportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the pending reports on a post or comment. When <paramref name="includeNested"/> is true the result also
        /// includes pending reports on comments that are deleted together with the content (a post's comments, or a
        /// comment's replies), because those reports lose their content link when the content is removed.
        /// </summary>
        /// <param name="postId">The reported post, or null.</param>
        /// <param name="commentId">The reported comment, or null.</param>
        /// <param name="includeNested">Whether to include reports on content that is deleted together with this content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The pending reports, oldest first.</returns>
        Task<List<GroupPendingReportRef>> GetPendingReportsAsync(int? postId, int? commentId, bool includeNested, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically changes a report's status only when it is currently in one of the allowed statuses.
        /// </summary>
        /// <param name="reportId">The report ID.</param>
        /// <param name="allowedFrom">The statuses the report may currently have.</param>
        /// <param name="status">The new status.</param>
        /// <param name="reviewerId">The reviewer to record.</param>
        /// <param name="reviewedAtUtc">The review time to record.</param>
        /// <param name="notes">The reviewer notes to record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated report, or null when the report was not in an allowed status.</returns>
        Task<GroupContentReport?> TryTransitionAsync(int reportId, IReadOnlyCollection<ReportStatus> allowedFrom, ReportStatus status, string? reviewerId, DateTime? reviewedAtUtc, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a user's display name.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The display name, or null when the user does not exist.</returns>
        Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
    }
}
