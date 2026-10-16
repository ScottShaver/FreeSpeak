using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// System moderator dashboard operations for feed content reports: queue, detail, status changes and content
    /// removal. Reporter identities are only ever returned here, to system moderators and administrators.
    /// </summary>
    public class SystemModerationService : ISystemModerationService
    {
        /// <summary>The notification template key used to tell an author their content was removed.</summary>
        public const string ContentRemovedTemplateKey = "FeedContentRemoved";

        /// <summary>The maximum length of a queue excerpt.</summary>
        public const int ExcerptLength = 200;

        private const int MaxPageSize = 100;
        private const int ClosedWindowDays = 7;
        private const string SystemAdministratorRole = "SystemAdministrator";
        private const string SystemModeratorRole = "SystemModerator";

        private static readonly ReportStatus[] PendingStatuses = { ReportStatus.NotReviewed, ReportStatus.UnderReview };

        private readonly IFeedContentReportRepository _repository;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly NotificationService _notificationService;
        private readonly IFeedContentRemoval _contentRemoval;
        private readonly HtmlSanitizationService _sanitizer;
        private readonly IOptionsMonitor<ModerationSettings> _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SystemModerationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemModerationService"/> class.
        /// </summary>
        /// <param name="repository">Repository for reports and reported content.</param>
        /// <param name="roleService">Service used to check system roles.</param>
        /// <param name="auditLogRepository">Audit log for moderator decisions and removals.</param>
        /// <param name="notificationService">Service used to notify reporters and authors.</param>
        /// <param name="contentRemoval">Service that deletes feed posts and comments.</param>
        /// <param name="sanitizer">Sanitizer applied to content shown in the detail view.</param>
        /// <param name="settings">Live moderation settings (stale threshold).</param>
        /// <param name="timeProvider">Clock used for server-set timestamps.</param>
        /// <param name="logger">Logger.</param>
        public SystemModerationService(
            IFeedContentReportRepository repository,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            NotificationService notificationService,
            IFeedContentRemoval contentRemoval,
            HtmlSanitizationService sanitizer,
            IOptionsMonitor<ModerationSettings> settings,
            TimeProvider timeProvider,
            ILogger<SystemModerationService> logger)
        {
            _repository = repository;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _notificationService = notificationService;
            _contentRemoval = contentRemoval;
            _sanitizer = sanitizer;
            _settings = settings;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ModerationOverview?> GetOverviewAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return null;
            }

            try
            {
                var now = Now();
                var days = StaleDays();
                var (notReviewed, underReview, closed, stale) = await _repository.GetOverviewCountsAsync(
                    now.AddDays(-days), now.AddDays(-ClosedWindowDays), cancellationToken);
                return new ModerationOverview(notReviewed, underReview, closed, stale, days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading moderation overview for user {UserId}", userId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<FeedReportQueuePage> GetFeedReportQueueAsync(string userId, FeedReportQueueFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return new FeedReportQueuePage(Array.Empty<FeedReportQueueItem>(), 0);
            }

            var staleBefore = Now().AddDays(-StaleDays());
            var (rows, total) = await _repository.GetQueueAsync(filter, staleBefore, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);
            var items = rows.Select(r => new FeedReportQueueItem(
                r.Id,
                r.CommentId.HasValue ? FeedReportContentType.Comment : FeedReportContentType.Post,
                r.PostId,
                r.CommentId,
                r.Reason,
                r.Status,
                r.CreatedAt,
                PlainTextExcerpt.Create(r.Content, ExcerptLength),
                r.ContentAuthorId,
                DisplayName(r.AuthorName),
                r.ReporterId,
                DisplayName(r.ReporterName),
                r.OtherPendingReports,
                r.CreatedAt < staleBefore)).ToList();
            return new FeedReportQueuePage(items, total);
        }

        /// <inheritdoc/>
        public async Task<FeedReportDetail?> GetFeedReportDetailAsync(string userId, int reportId, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return null;
            }

            var report = await _repository.GetByIdAsync(reportId, cancellationToken);
            if (report == null)
            {
                return null;
            }

            var content = await _repository.GetContentSnapshotAsync(report.PostId, report.CommentId, cancellationToken);
            var others = await _repository.GetOtherReportsAsync(report.PostId, report.CommentId, report.Id, cancellationToken);
            var authorReports = await _repository.CountReportsAgainstAuthorAsync(report.ContentAuthorId, report.Id, cancellationToken);
            var reporter = DisplayName(await _repository.GetUserDisplayNameAsync(report.ReporterId, cancellationToken));

            var staleBefore = Now().AddDays(-StaleDays());
            var isPending = report.Status is ReportStatus.NotReviewed or ReportStatus.UnderReview;
            var isOwn = string.Equals(report.ContentAuthorId, userId, StringComparison.Ordinal)
                || string.Equals(report.ReporterId, userId, StringComparison.Ordinal);

            return new FeedReportDetail(
                report.Id,
                report.CommentId.HasValue ? FeedReportContentType.Comment : FeedReportContentType.Post,
                report.PostId,
                report.CommentId,
                report.Reason,
                report.Description,
                report.Status,
                report.CreatedAt,
                report.ReviewedAt,
                report.ReviewerNotes,
                report.ReporterId,
                reporter,
                report.ContentAuthorId,
                DisplayName(content?.AuthorName),
                content == null ? string.Empty : _sanitizer.SanitizeAndFormatContent(content.Content),
                content?.CreatedAt ?? report.CreatedAt,
                others,
                authorReports,
                isPending && report.CreatedAt < staleBefore,
                isPending && !isOwn && content != null);
        }

        /// <inheritdoc/>
        public async Task<FeedReportResult> MarkUnderReviewAsync(string userId, int reportId, CancellationToken cancellationToken = default)
        {
            var gate = await GateAsync(userId, reportId, cancellationToken);
            if (gate.Error != FeedReportError.None)
            {
                return FeedReportResult.Fail(gate.Error);
            }

            try
            {
                var updated = await _repository.TryTransitionAsync(reportId, new[] { ReportStatus.NotReviewed }, ReportStatus.UnderReview, userId, Now(), null, cancellationToken);
                if (updated == null)
                {
                    return FeedReportResult.Fail(FeedReportError.AlreadyReviewed);
                }

                await LogReviewAuditAsync(userId, updated, ReportStatus.UnderReview);
                return FeedReportResult.Ok(updated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking feed report {ReportId} under review", reportId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <inheritdoc/>
        public Task<FeedReportResult> DismissAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default)
            => CloseAsync(userId, reportId, ReportStatus.Dismissed, notes, cancellationToken);

        /// <inheritdoc/>
        public Task<FeedReportResult> ResolveAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default)
            => CloseAsync(userId, reportId, ReportStatus.Resolved, notes, cancellationToken);

        /// <inheritdoc/>
        public async Task<FeedReportResult> RemoveContentAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default)
        {
            var gate = await GateAsync(userId, reportId, cancellationToken);
            if (gate.Error != FeedReportError.None)
            {
                return FeedReportResult.Fail(gate.Error);
            }

            var trimmedNotes = TrimNotes(notes);
            if (trimmedNotes is { Length: > FeedContentReportService.MaxTextLength })
            {
                return FeedReportResult.Fail(FeedReportError.DescriptionTooLong);
            }

            var report = gate.Report!;
            try
            {
                var content = await _repository.GetContentSnapshotAsync(report.PostId, report.CommentId, cancellationToken);
                if (content == null)
                {
                    return FeedReportResult.Fail(FeedReportError.ContentNotFound);
                }

                // Reports on the content are deleted with it, so collect every reporter before removal
                var reporters = await _repository.GetPendingReportersAsync(report.PostId, report.CommentId, cancellationToken);

                // Claim the report first so two moderators cannot both remove the same content
                var claimed = await _repository.TryTransitionAsync(reportId, PendingStatuses, ReportStatus.ActionTaken, userId, Now(), trimmedNotes, cancellationToken);
                if (claimed == null)
                {
                    return FeedReportResult.Fail(FeedReportError.AlreadyReviewed);
                }

                var removed = report.CommentId.HasValue
                    ? (await _contentRemoval.RemoveCommentAsModeratorAsync(report.CommentId.Value, userId)).Success
                    : (await _contentRemoval.RemovePostAsModeratorAsync(report.PostId!.Value, userId)).Success;

                if (!removed)
                {
                    await _repository.TryTransitionAsync(reportId, new[] { ReportStatus.ActionTaken }, report.Status, report.ReviewerId, report.ReviewedAt, report.ReviewerNotes, cancellationToken);
                    return FeedReportResult.Fail(FeedReportError.RemovalFailed);
                }

                await LogRemovalAuditAsync(userId, report, content, trimmedNotes);

                foreach (var (_, reporterId) in reporters.DistinctBy(r => r.ReporterId))
                {
                    await NotifyAsync(reporterId, "Your report was reviewed.", FeedContentReportService.ReviewedTemplateKey, new { ReportId = reportId });
                }

                await NotifyAsync(content.AuthorId, "Your content was removed for violating the community guidelines.", ContentRemovedTemplateKey, new { });

                _logger.LogInformation("Feed content for report {ReportId} removed by system moderator {UserId}", reportId, userId);
                return FeedReportResult.Ok(reportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing content for feed report {ReportId}", reportId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <summary>
        /// Closes a report with the given outcome, closes every other open report on the same content the same way,
        /// audits each decision and notifies each reporter.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="status">The outcome (dismissed or resolved).</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        private async Task<FeedReportResult> CloseAsync(string userId, int reportId, ReportStatus status, string? notes, CancellationToken cancellationToken)
        {
            var gate = await GateAsync(userId, reportId, cancellationToken);
            if (gate.Error != FeedReportError.None)
            {
                return FeedReportResult.Fail(gate.Error);
            }

            var trimmedNotes = TrimNotes(notes);
            if (trimmedNotes is { Length: > FeedContentReportService.MaxTextLength })
            {
                return FeedReportResult.Fail(FeedReportError.DescriptionTooLong);
            }

            try
            {
                var now = Now();
                var updated = await _repository.TryTransitionAsync(reportId, PendingStatuses, status, userId, now, trimmedNotes, cancellationToken);
                if (updated == null)
                {
                    return FeedReportResult.Fail(FeedReportError.AlreadyReviewed);
                }

                var related = await _repository.CloseOtherPendingAsync(updated.PostId, updated.CommentId, updated.Id, status, userId, now, trimmedNotes, cancellationToken);

                foreach (var closed in new[] { updated }.Concat(related))
                {
                    await LogReviewAuditAsync(userId, closed, status);
                    await NotifyAsync(closed.ReporterId, "Your report was reviewed.", FeedContentReportService.ReviewedTemplateKey, new { ReportId = closed.Id });
                }

                return FeedReportResult.Ok(updated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing feed report {ReportId}", reportId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <summary>
        /// Runs the checks shared by every action: the caller's role, that the report exists and is still open,
        /// and that the moderator is not acting on their own content or their own report.
        /// </summary>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The report when allowed; otherwise the reason it was refused.</returns>
        private async Task<(FeedContentReport? Report, FeedReportError Error)> GateAsync(string userId, int reportId, CancellationToken cancellationToken)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return (null, FeedReportError.NotAuthorized);
            }

            FeedContentReport? report;
            try
            {
                report = await _repository.GetByIdAsync(reportId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading feed report {ReportId}", reportId);
                return (null, FeedReportError.Failed);
            }

            if (report == null)
            {
                return (null, FeedReportError.ReportNotFound);
            }

            if (report.Status is not (ReportStatus.NotReviewed or ReportStatus.UnderReview))
            {
                return (null, FeedReportError.AlreadyReviewed);
            }

            if (string.Equals(report.ContentAuthorId, userId, StringComparison.Ordinal))
            {
                return (null, FeedReportError.OwnContent);
            }

            if (string.Equals(report.ReporterId, userId, StringComparison.Ordinal))
            {
                return (null, FeedReportError.OwnReport);
            }

            return (report, FeedReportError.None);
        }

        /// <summary>
        /// Gets the configured stale threshold in days, never less than one.
        /// </summary>
        /// <returns>The threshold in days.</returns>
        private int StaleDays() => Math.Max(1, _settings.CurrentValue.StaleReportDays);

        /// <summary>
        /// Gets the current UTC time from the injected clock.
        /// </summary>
        /// <returns>The current UTC time.</returns>
        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Normalizes a database-provided name, trimming it and substituting a placeholder when empty.
        /// </summary>
        /// <param name="name">The raw name.</param>
        /// <returns>A non-empty display name.</returns>
        private static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? "?" : name.Trim();

        /// <summary>
        /// Trims optional notes, mapping empty text to null.
        /// </summary>
        /// <param name="notes">The raw notes.</param>
        /// <returns>The trimmed notes or null.</returns>
        private static string? TrimNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        /// <summary>
        /// Determines whether the user holds the SystemModerator or SystemAdministrator role.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>True when the user may use the moderator dashboard.</returns>
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
        /// Audits a report status decision.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="report">The updated report.</param>
        /// <param name="decision">The decision applied.</param>
        private Task LogReviewAuditAsync(string userId, FeedContentReport report, ReportStatus decision)
            => LogAuditAsync(userId, ActionCategory.SystemModeratorReviewReport, new SystemModeratorReviewReportDetails
            {
                OperationType = OperationTypeEnum.Update.ToString(),
                ReportId = report.Id,
                PostId = report.PostId,
                CommentId = report.CommentId,
                Decision = decision.ToString()
            });

        /// <summary>
        /// Audits the removal of a feed post (category 26) or comment (category 27).
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="report">The report that led to the removal.</param>
        /// <param name="content">The removed content as it was before deletion.</param>
        /// <param name="notes">The moderator's notes.</param>
        private Task LogRemovalAuditAsync(string userId, FeedContentReport report, FeedReportContentSnapshot content, string? notes)
        {
            var summary = PlainTextExcerpt.Create(content.Content, ExcerptLength);
            if (report.CommentId.HasValue)
            {
                return LogAuditAsync(userId, ActionCategory.SystemModeratorRemovePostComment, new SystemModeratorRemovePostCommentDetails
                {
                    OperationType = OperationTypeEnum.Delete.ToString(),
                    PostId = content.PostId,
                    CommentId = report.CommentId.Value,
                    PostType = "FeedPost",
                    CommentAuthorId = content.AuthorId,
                    CommentAuthorDisplayName = content.AuthorName,
                    ContentSummary = summary,
                    ViolationReason = report.Reason.ToString(),
                    ModeratorNotes = notes
                });
            }

            return LogAuditAsync(userId, ActionCategory.SystemModeratorRemovePost, new SystemModeratorRemovePostDetails
            {
                OperationType = OperationTypeEnum.Delete.ToString(),
                PostId = content.PostId,
                PostType = "FeedPost",
                PostAuthorId = content.AuthorId,
                PostAuthorDisplayName = content.AuthorName,
                ContentSummary = summary,
                ViolationReason = report.Reason.ToString(),
                ModeratorNotes = notes
            });
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
        /// Sends a neutral system notification. Failures are logged and never fail the moderator action.
        /// </summary>
        /// <param name="userId">The recipient's user ID.</param>
        /// <param name="message">The English fallback message.</param>
        /// <param name="templateKey">The localization template key.</param>
        /// <param name="data">Optional notification data.</param>
        private async Task NotifyAsync(string userId, string message, string templateKey, object data)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(userId, NotificationType.System, message, data, templateKey: templateKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending {Template} notification to user {UserId}", templateKey, userId);
            }
        }
    }
}
