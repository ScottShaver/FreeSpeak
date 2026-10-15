using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Handles reports of feed posts and feed comments: submission by users and review by system moderators.
    /// </summary>
    public interface IFeedContentReportService
    {
        /// <summary>
        /// Reports a feed post.
        /// </summary>
        /// <param name="postId">The post to report.</param>
        /// <param name="reporterId">The reporting user's ID.</param>
        /// <param name="reason">The reason; group-specific reasons are rejected.</param>
        /// <param name="description">Optional context, at most 2000 characters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome of the submission.</returns>
        Task<FeedReportResult> ReportPostAsync(int postId, string reporterId, FreeSpeakWeb.Data.ReportReason reason, string? description, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports a feed comment.
        /// </summary>
        /// <param name="commentId">The comment to report.</param>
        /// <param name="reporterId">The reporting user's ID.</param>
        /// <param name="reason">The reason; group-specific reasons are rejected.</param>
        /// <param name="description">Optional context, at most 2000 characters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome of the submission.</returns>
        Task<FeedReportResult> ReportCommentAsync(int commentId, string reporterId, FreeSpeakWeb.Data.ReportReason reason, string? description, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the user already reported the given post.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="postId">The post ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the user already reported the post.</returns>
        Task<bool> HasUserReportedPostAsync(string userId, int postId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the user already reported the given comment.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="commentId">The comment ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the user already reported the comment.</returns>
        Task<bool> HasUserReportedCommentAsync(string userId, int commentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets pending reports for the moderator queue. Only system moderators and administrators may call this.
        /// </summary>
        /// <param name="requestingUserId">The requesting user's ID.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page; empty when the user is not authorized.</returns>
        Task<FeedContentReportPage> GetPendingReportsAsync(string requestingUserId, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the number of pending reports. Only system moderators and administrators may call this.
        /// </summary>
        /// <param name="requestingUserId">The requesting user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The count, or 0 when the user is not authorized.</returns>
        Task<int> GetPendingReportCountAsync(string requestingUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a single report. Only system moderators and administrators may call this.
        /// </summary>
        /// <param name="requestingUserId">The requesting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The report, or null when it does not exist or the user is not authorized.</returns>
        Task<FeedContentReportDto?> GetReportByIdAsync(string requestingUserId, int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dismisses a report as not violating any rules and notifies the reporter.
        /// </summary>
        /// <param name="reviewerId">The reviewing user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> DismissReportAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks that action was taken on the reported content and notifies the reporter.
        /// </summary>
        /// <param name="reviewerId">The reviewing user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> MarkActionTakenAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a report with no action required and notifies the reporter.
        /// </summary>
        /// <param name="reviewerId">The reviewing user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> ResolveReportAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default);
    }
}
