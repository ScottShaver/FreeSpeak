using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// System moderator dashboard operations on feed content reports. Every method re-checks on the server that
    /// the caller is a system moderator or administrator; ordinary users and group-level roles are refused.
    /// </summary>
    public interface ISystemModerationService
    {
        /// <summary>
        /// Gets the dashboard overview counts.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The overview, or null when the user may not use the dashboard.</returns>
        Task<ModerationOverview?> GetOverviewAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one page of the feed report queue (pending reports; oldest first unless the filter says otherwise).
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="filter">The filters and ordering.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page; empty when the user may not use the dashboard.</returns>
        Task<FeedReportQueuePage> GetFeedReportQueueAsync(string userId, FeedReportQueueFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the detail of one feed report.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The detail, or null when the report does not exist or the user is not authorized.</returns>
        Task<FeedReportDetail?> GetFeedReportDetailAsync(string userId, int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a not-reviewed report as under review.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> MarkUnderReviewAsync(string userId, int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dismisses a report, and every other open report on the same content, as unfounded.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> DismissAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a report, and every other open report on the same content, without removing the content.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> ResolveAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the reported content, records the action in the audit log, and notifies the reporters and the author.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> RemoveContentAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);
    }
}
