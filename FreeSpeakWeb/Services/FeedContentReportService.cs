using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Handles reports of feed posts and feed comments. Users report content they can see; only system
    /// moderators and administrators can list, read and act on reports. Reported content is never auto-hidden.
    /// </summary>
    public class FeedContentReportService : IFeedContentReportService
    {
        /// <summary>Maximum length of a reporter description or reviewer note.</summary>
        public const int MaxTextLength = 2000;

        /// <summary>The notification template key used to tell a reporter their report was reviewed.</summary>
        public const string ReviewedTemplateKey = "FeedReportReviewed";

        private const int MaxPageSize = 100;
        private const string SystemAdministratorRole = "SystemAdministrator";
        private const string SystemModeratorRole = "SystemModerator";

        private readonly IFeedContentReportRepository _repository;
        private readonly IFriendshipRepository _friendshipRepository;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly NotificationService _notificationService;
        private readonly IOptions<FeedReportSettings> _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<FeedContentReportService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedContentReportService"/> class.
        /// </summary>
        /// <param name="repository">Repository for reports and reported content.</param>
        /// <param name="friendshipRepository">Repository used to evaluate friends-only visibility.</param>
        /// <param name="roleService">Service used to check system roles.</param>
        /// <param name="auditLogRepository">Audit log for submissions and reviewer decisions.</param>
        /// <param name="notificationService">Service used to notify reporters of review outcomes.</param>
        /// <param name="settings">Feed reporting settings (rate limit).</param>
        /// <param name="timeProvider">Clock used for server-set timestamps.</param>
        /// <param name="logger">Logger.</param>
        public FeedContentReportService(
            IFeedContentReportRepository repository,
            IFriendshipRepository friendshipRepository,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            NotificationService notificationService,
            IOptions<FeedReportSettings> settings,
            TimeProvider timeProvider,
            ILogger<FeedContentReportService> logger)
        {
            _repository = repository;
            _friendshipRepository = friendshipRepository;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _notificationService = notificationService;
            _settings = settings;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Gets the report reasons that are valid for feed content (all reasons except group-specific ones).
        /// </summary>
        public static IReadOnlyList<ReportReason> AllowedReasons { get; } = Enum.GetValues<ReportReason>()
            .Where(r => r != ReportReason.ViolatesGroupRule && r != ReportReason.OffTopic)
            .ToList();

        /// <inheritdoc/>
        public Task<FeedReportResult> ReportPostAsync(int postId, string reporterId, ReportReason reason, string? description, CancellationToken cancellationToken = default)
            => SubmitAsync(postId, null, reporterId, reason, description, cancellationToken);

        /// <inheritdoc/>
        public Task<FeedReportResult> ReportCommentAsync(int commentId, string reporterId, ReportReason reason, string? description, CancellationToken cancellationToken = default)
            => SubmitAsync(null, commentId, reporterId, reason, description, cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> HasUserReportedPostAsync(string userId, int postId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            try
            {
                return await _repository.HasReportedAsync(userId, postId, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking whether user {UserId} reported post {PostId}", userId, postId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> HasUserReportedCommentAsync(string userId, int commentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            try
            {
                return await _repository.HasReportedAsync(userId, null, commentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking whether user {UserId} reported comment {CommentId}", userId, commentId);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<FeedContentReportPage> GetPendingReportsAsync(string requestingUserId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(requestingUserId))
            {
                return new FeedContentReportPage(Array.Empty<FeedContentReportDto>(), 0);
            }

            var (items, total) = await _repository.GetPendingPagedAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);
            return new FeedContentReportPage(items.Select(ToDto).ToList(), total);
        }

        /// <inheritdoc/>
        public async Task<int> GetPendingReportCountAsync(string requestingUserId, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(requestingUserId))
            {
                return 0;
            }

            return await _repository.GetPendingCountAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FeedContentReportDto?> GetReportByIdAsync(string requestingUserId, int reportId, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(requestingUserId))
            {
                return null;
            }

            var report = await _repository.GetByIdAsync(reportId, cancellationToken);
            return report == null ? null : ToDto(report);
        }

        /// <inheritdoc/>
        public Task<FeedReportResult> DismissReportAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default)
            => ReviewAsync(reviewerId, reportId, ReportStatus.Dismissed, notes, cancellationToken);

        /// <inheritdoc/>
        public Task<FeedReportResult> MarkActionTakenAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default)
            => ReviewAsync(reviewerId, reportId, ReportStatus.ActionTaken, notes, cancellationToken);

        /// <inheritdoc/>
        public Task<FeedReportResult> ResolveReportAsync(string reviewerId, int reportId, string? notes = null, CancellationToken cancellationToken = default)
            => ReviewAsync(reviewerId, reportId, ReportStatus.Resolved, notes, cancellationToken);

        /// <summary>
        /// Validates and stores a report against a post or a comment.
        /// </summary>
        /// <param name="postId">The post ID, or null when reporting a comment.</param>
        /// <param name="commentId">The comment ID, or null when reporting a post.</param>
        /// <param name="reporterId">The reporter's user ID.</param>
        /// <param name="reason">The report reason.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome of the submission.</returns>
        private async Task<FeedReportResult> SubmitAsync(int? postId, int? commentId, string reporterId, ReportReason reason, string? description, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reporterId))
            {
                return FeedReportResult.Fail(FeedReportError.NotAuthenticated);
            }

            if (!AllowedReasons.Contains(reason))
            {
                return FeedReportResult.Fail(FeedReportError.InvalidReason);
            }

            var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (trimmedDescription is { Length: > MaxTextLength })
            {
                return FeedReportResult.Fail(FeedReportError.DescriptionTooLong);
            }

            try
            {
                var target = commentId.HasValue
                    ? await _repository.GetCommentTargetAsync(commentId.Value, cancellationToken)
                    : await _repository.GetPostTargetAsync(postId!.Value, cancellationToken);

                // Missing content and content the reporter cannot see are indistinguishable
                if (target == null || !await CanViewAsync(target, reporterId))
                {
                    return FeedReportResult.Fail(FeedReportError.ContentNotFound);
                }

                if (string.Equals(target.ContentAuthorId, reporterId, StringComparison.Ordinal))
                {
                    return FeedReportResult.Fail(FeedReportError.OwnContent);
                }

                if (await _repository.HasReportedAsync(reporterId, postId, commentId, cancellationToken))
                {
                    return FeedReportResult.Fail(FeedReportError.DuplicateReport);
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var recent = await _repository.CountReportsSinceAsync(reporterId, now.AddHours(-1), cancellationToken);
                if (recent >= _settings.Value.MaxReportsPerUserPerHour)
                {
                    return FeedReportResult.Fail(FeedReportError.RateLimited);
                }

                var report = new FeedContentReport
                {
                    PostId = postId,
                    CommentId = commentId,
                    ContentAuthorId = target.ContentAuthorId,
                    ReporterId = reporterId,
                    Reason = reason,
                    Description = trimmedDescription,
                    Status = ReportStatus.NotReviewed,
                    CreatedAt = now
                };

                if (!await _repository.AddAsync(report, cancellationToken))
                {
                    return FeedReportResult.Fail(FeedReportError.DuplicateReport);
                }

                await LogAuditAsync(reporterId, ActionCategory.UserReportContent, new UserReportContentDetails
                {
                    OperationType = OperationTypeEnum.Create.ToString(),
                    ReportId = report.Id,
                    PostId = postId,
                    CommentId = commentId,
                    ContentAuthorId = target.ContentAuthorId,
                    Reason = reason.ToString()
                });

                _logger.LogInformation("Feed content reported by {ReporterId}: report {ReportId} ({Reason})", reporterId, report.Id, reason);
                return FeedReportResult.Ok(report.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feed report by user {ReporterId}", reporterId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <summary>
        /// Records a reviewer decision, audits it and notifies the reporter.
        /// </summary>
        /// <param name="reviewerId">The reviewer's user ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="status">The decision status.</param>
        /// <param name="notes">Optional reviewer notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        private async Task<FeedReportResult> ReviewAsync(string reviewerId, int reportId, ReportStatus status, string? notes, CancellationToken cancellationToken)
        {
            if (!await IsSystemModeratorOrAdminAsync(reviewerId))
            {
                return FeedReportResult.Fail(FeedReportError.NotAuthorized);
            }

            var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            if (trimmedNotes is { Length: > MaxTextLength })
            {
                return FeedReportResult.Fail(FeedReportError.DescriptionTooLong);
            }

            try
            {
                var existing = await _repository.GetByIdAsync(reportId, cancellationToken);
                if (existing == null)
                {
                    return FeedReportResult.Fail(FeedReportError.ReportNotFound);
                }

                if (existing.Status is not (ReportStatus.NotReviewed or ReportStatus.UnderReview))
                {
                    return FeedReportResult.Fail(FeedReportError.AlreadyReviewed);
                }

                var updated = await _repository.UpdateReviewAsync(reportId, status, reviewerId, _timeProvider.GetUtcNow().UtcDateTime, trimmedNotes, cancellationToken);
                if (updated == null)
                {
                    return FeedReportResult.Fail(FeedReportError.ReportNotFound);
                }

                await LogAuditAsync(reviewerId, ActionCategory.SystemModeratorReviewReport, new SystemModeratorReviewReportDetails
                {
                    OperationType = OperationTypeEnum.Update.ToString(),
                    ReportId = updated.Id,
                    PostId = updated.PostId,
                    CommentId = updated.CommentId,
                    Decision = status.ToString()
                });

                await NotifyReporterAsync(updated);
                return FeedReportResult.Ok(updated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing feed report {ReportId}", reportId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <summary>
        /// Tells the reporter their report was reviewed. The message is neutral and carries no moderator
        /// identity or action details. Failures are logged and never fail the decision.
        /// </summary>
        /// <param name="report">The reviewed report.</param>
        private async Task NotifyReporterAsync(FeedContentReport report)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    report.ReporterId,
                    NotificationType.System,
                    "Your report was reviewed.",
                    new { ReportId = report.Id },
                    templateKey: ReviewedTemplateKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying reporter about feed report {ReportId}", report.Id);
            }
        }

        /// <summary>
        /// Determines whether a viewer is allowed to see the post that holds the reported content.
        /// </summary>
        /// <param name="target">The reported content and its parent post details.</param>
        /// <param name="viewerId">The viewer's user ID.</param>
        /// <returns>True when the viewer can see the content.</returns>
        private async Task<bool> CanViewAsync(FeedReportTarget target, string viewerId)
        {
            if (string.Equals(target.PostAuthorId, viewerId, StringComparison.Ordinal))
            {
                return true;
            }

            return target.Audience switch
            {
                AudienceType.Public => true,
                AudienceType.FriendsOnly => string.Equals(target.PostFriendId, viewerId, StringComparison.Ordinal)
                    || await _friendshipRepository.AreFriendsAsync(target.PostAuthorId, viewerId),
                _ => string.Equals(target.PostFriendId, viewerId, StringComparison.Ordinal) && target.Audience != AudienceType.MeOnly
            };
        }

        /// <summary>
        /// Determines whether the user holds the SystemModerator or SystemAdministrator role.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>True when the user may manage feed reports.</returns>
        private async Task<bool> IsSystemModeratorOrAdminAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return await _roleService.IsUserInRoleAsync(userId, SystemModeratorRole)
                || await _roleService.IsUserInRoleAsync(userId, SystemAdministratorRole);
        }

        /// <summary>
        /// Writes an audit entry. Audit failures are logged and never fail the operation.
        /// </summary>
        /// <typeparam name="TDetails">The details type.</typeparam>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="category">The audit category.</param>
        /// <param name="details">The audit details.</param>
        private async Task LogAuditAsync<TDetails>(string userId, ActionCategory category, TDetails details) where TDetails : class
        {
            try
            {
                await _auditLogRepository.LogActionAsync(userId, category, details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit entry {Category} for user {UserId}", category, userId);
            }
        }

        /// <summary>
        /// Maps a report entity to its moderator-facing DTO.
        /// </summary>
        /// <param name="report">The report entity.</param>
        /// <returns>The DTO.</returns>
        private static FeedContentReportDto ToDto(FeedContentReport report) => new(
            report.Id,
            report.PostId,
            report.CommentId,
            report.ContentAuthorId,
            report.ReporterId,
            report.Reason,
            report.Description,
            report.Status,
            report.CreatedAt,
            report.ReviewerId,
            report.ReviewedAt,
            report.ReviewerNotes);
    }
}
