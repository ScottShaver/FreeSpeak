using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// System moderator dashboard operations for group content reports across all groups. System moderators and
    /// administrators act as a backup for group moderators. Every action is audited globally, written to the group
    /// moderation log (marked as a system-level action) and notifies the reporter and, when content is removed, the
    /// author.
    /// </summary>
    public class SystemGroupReportService : ISystemGroupReportService
    {
        private const int MaxPageSize = 100;
        private const int ClosedWindowDays = 7;
        private const string SystemAdministratorRole = "SystemAdministrator";
        private const string SystemModeratorRole = "SystemModerator";
        private const string GroupPostType = "GroupPost";
        private const string ReviewedMessage = "Your report was reviewed.";
        private const string RemovedMessage = "Your content was removed for violating the community guidelines.";

        private static readonly ReportStatus[] PendingStatuses = { ReportStatus.NotReviewed, ReportStatus.UnderReview };

        private readonly IGroupReportModerationRepository _repository;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IGroupModerationLogService _moderationLog;
        private readonly NotificationService _notificationService;
        private readonly IGroupContentRemoval _contentRemoval;
        private readonly HtmlSanitizationService _sanitizer;
        private readonly IOptionsMonitor<ModerationSettings> _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SystemGroupReportService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemGroupReportService"/> class.
        /// </summary>
        /// <param name="repository">Repository for group reports and reported content.</param>
        /// <param name="roleService">Service used to check system roles.</param>
        /// <param name="auditLogRepository">Audit log for moderator decisions and removals.</param>
        /// <param name="moderationLog">Per-group moderation log that group moderators read.</param>
        /// <param name="notificationService">Service used to notify reporters and authors.</param>
        /// <param name="contentRemoval">Service that deletes group posts and comments.</param>
        /// <param name="sanitizer">Sanitizer applied to content shown in the detail view.</param>
        /// <param name="settings">Live moderation settings (stale threshold).</param>
        /// <param name="timeProvider">Clock used for server-set timestamps.</param>
        /// <param name="logger">Logger.</param>
        public SystemGroupReportService(
            IGroupReportModerationRepository repository,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            IGroupModerationLogService moderationLog,
            NotificationService notificationService,
            IGroupContentRemoval contentRemoval,
            HtmlSanitizationService sanitizer,
            IOptionsMonitor<ModerationSettings> settings,
            TimeProvider timeProvider,
            ILogger<SystemGroupReportService> logger)
        {
            _repository = repository;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _moderationLog = moderationLog;
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

            var now = Now();
            var days = StaleDays();
            var (notReviewed, underReview, closed, stale) = await _repository.GetOverviewCountsAsync(
                now.AddDays(-days), now.AddDays(-ClosedWindowDays), cancellationToken);
            return new ModerationOverview(notReviewed, underReview, closed, stale, days);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<GroupReportGroupOption>> GetGroupOptionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return Array.Empty<GroupReportGroupOption>();
            }

            return await _repository.GetGroupsWithPendingReportsAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GroupReportQueuePage> GetQueueAsync(string userId, GroupReportQueueFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return new GroupReportQueuePage(Array.Empty<GroupReportQueueItem>(), 0);
            }

            var staleBefore = Now().AddDays(-StaleDays());
            var (rows, total) = await _repository.GetQueueAsync(filter, staleBefore, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);
            var items = rows.Select(r => new GroupReportQueueItem(
                r.Id,
                r.GroupId,
                r.GroupName,
                r.CommentId.HasValue ? FeedReportContentType.Comment : FeedReportContentType.Post,
                r.PostId,
                r.CommentId,
                r.Reason,
                r.Status,
                r.CreatedAt,
                PlainTextExcerpt.Create(r.Content, SystemModerationService.ExcerptLength),
                r.ContentAuthorId ?? string.Empty,
                DisplayName(r.AuthorName),
                r.ReporterId,
                DisplayName(r.ReporterName),
                r.OtherPendingReports,
                r.CreatedAt < staleBefore)).ToList();
            return new GroupReportQueuePage(items, total);
        }

        /// <inheritdoc/>
        public async Task<GroupReportDetail?> GetDetailAsync(string userId, int reportId, CancellationToken cancellationToken = default)
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
            var authorReports = content == null ? 0 : await _repository.CountReportsAgainstAuthorAsync(content.AuthorId, report.Id, cancellationToken);
            var reporter = DisplayName(await _repository.GetUserDisplayNameAsync(report.ReporterId, cancellationToken));

            var staleBefore = Now().AddDays(-StaleDays());
            var isPending = report.Status is ReportStatus.NotReviewed or ReportStatus.UnderReview;
            var isOwn = string.Equals(content?.AuthorId, userId, StringComparison.Ordinal)
                || string.Equals(report.ReporterId, userId, StringComparison.Ordinal);

            return new GroupReportDetail(
                report.Id,
                report.GroupId,
                report.Group?.Name ?? content?.GroupName ?? string.Empty,
                report.CommentId.HasValue ? FeedReportContentType.Comment : FeedReportContentType.Post,
                report.PostId,
                report.CommentId,
                report.Reason,
                report.ViolatedRule?.Title,
                report.Description,
                report.Status,
                report.CreatedAt,
                report.ReviewedAt,
                report.ReviewerNotes,
                report.ReporterId,
                reporter,
                content?.AuthorId ?? string.Empty,
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

                await LogReviewAuditAsync(userId, updated.Id, updated.GroupId, updated.PostId, updated.CommentId, ReportStatus.UnderReview, gate.Content?.GroupName);
                return FeedReportResult.Ok(updated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking group report {ReportId} under review", reportId);
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
            var content = gate.Content;
            if (content == null)
            {
                return FeedReportResult.Fail(FeedReportError.ContentNotFound);
            }

            try
            {
                // Reports on the removed content (and on comments deleted with it) lose their link when it is
                // deleted, so collect them and close them first
                var affected = await _repository.GetPendingReportsAsync(report.PostId, report.CommentId, true, cancellationToken);

                // Claim the report first so two moderators cannot both remove the same content
                var now = Now();
                var claimed = await _repository.TryTransitionAsync(reportId, PendingStatuses, ReportStatus.ActionTaken, userId, now, trimmedNotes, cancellationToken);
                if (claimed == null)
                {
                    return FeedReportResult.Fail(FeedReportError.AlreadyReviewed);
                }

                var closedOthers = new List<GroupPendingReportRef>();
                foreach (var other in affected.Where(a => a.Id != reportId))
                {
                    var resolved = await _repository.TryTransitionAsync(other.Id, PendingStatuses, ReportStatus.Resolved, userId, now, trimmedNotes, cancellationToken);
                    if (resolved != null)
                    {
                        closedOthers.Add(other);
                    }
                }

                var removed = report.CommentId.HasValue
                    ? (await _contentRemoval.RemoveCommentAsModeratorAsync(report.CommentId.Value, userId)).Success
                    : (await _contentRemoval.RemovePostAsModeratorAsync(report.PostId!.Value, userId)).Success;

                if (!removed)
                {
                    await _repository.TryTransitionAsync(reportId, new[] { ReportStatus.ActionTaken }, report.Status, report.ReviewerId, report.ReviewedAt, report.ReviewerNotes, cancellationToken);
                    foreach (var other in closedOthers)
                    {
                        await _repository.TryTransitionAsync(other.Id, new[] { ReportStatus.Resolved }, other.Status, other.ReviewerId, other.ReviewedAt, other.ReviewerNotes, cancellationToken);
                    }

                    return FeedReportResult.Fail(FeedReportError.RemovalFailed);
                }

                await LogRemovalAuditAsync(userId, report, content, trimmedNotes);
                await LogModerationAsync(userId, report.GroupId,
                    report.CommentId.HasValue ? GroupModerationAction.CommentRemoved : GroupModerationAction.PostRemoved,
                    report.CommentId.HasValue ? GroupModerationTargetType.Comment : GroupModerationTargetType.Post,
                    (report.CommentId ?? report.PostId)?.ToString(), content.AuthorId, content.Content, trimmedNotes);
                await LogModerationAsync(userId, report.GroupId, GroupModerationAction.ReportResolved, GroupModerationTargetType.Report,
                    report.Id.ToString(), content.AuthorId, content.Content, trimmedNotes);

                await LogReviewAuditAsync(userId, report.Id, report.GroupId, report.PostId, report.CommentId, ReportStatus.ActionTaken, content.GroupName);
                await NotifyAsync(report.ReporterId, ReviewedMessage, FeedContentReportService.ReviewedTemplateKey, new { ReportId = reportId });

                var reporters = new HashSet<string>(StringComparer.Ordinal) { report.ReporterId };
                var notifiedAuthors = new HashSet<string>(StringComparer.Ordinal) { content.AuthorId };
                foreach (var other in closedOthers)
                {
                    await LogReviewAuditAsync(userId, other.Id, report.GroupId, other.PostId, other.CommentId, ReportStatus.Resolved, content.GroupName);
                    await LogModerationAsync(userId, report.GroupId, GroupModerationAction.ReportResolved, GroupModerationTargetType.Report,
                        other.Id.ToString(), other.ContentAuthorId, null, trimmedNotes);
                    await NotifyAsync(other.ReporterId, ReviewedMessage, FeedContentReportService.ReviewedTemplateKey, new { ReportId = other.Id });
                    reporters.Add(other.ReporterId);
                }

                await NotifyAsync(content.AuthorId, RemovedMessage, SystemModerationService.ContentRemovedTemplateKey, new { });
                foreach (var authorId in closedOthers.Select(o => o.ContentAuthorId))
                {
                    if (!string.IsNullOrEmpty(authorId) && notifiedAuthors.Add(authorId))
                    {
                        await NotifyAsync(authorId, RemovedMessage, SystemModerationService.ContentRemovedTemplateKey, new { });
                    }
                }

                _logger.LogInformation("Group content for report {ReportId} removed by system moderator {UserId}", reportId, userId);
                return FeedReportResult.Ok(reportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing content for group report {ReportId}", reportId);
                return FeedReportResult.Fail(FeedReportError.Failed);
            }
        }

        /// <summary>
        /// Closes a report with the given outcome, closes every other open report on the same content the same way,
        /// audits each decision, writes the group moderation log and notifies each reporter.
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

                var others = await _repository.GetPendingReportsAsync(updated.PostId, updated.CommentId, false, cancellationToken);
                var closedOthers = new List<GroupPendingReportRef>();
                foreach (var other in others.Where(o => o.Id != updated.Id))
                {
                    var closed = await _repository.TryTransitionAsync(other.Id, PendingStatuses, status, userId, now, trimmedNotes, cancellationToken);
                    if (closed != null)
                    {
                        closedOthers.Add(other);
                    }
                }

                var action = status == ReportStatus.Dismissed ? GroupModerationAction.ReportDismissed : GroupModerationAction.ReportResolved;
                var authorId = gate.Content?.AuthorId;

                await LogReviewAuditAsync(userId, updated.Id, updated.GroupId, updated.PostId, updated.CommentId, status, gate.Content?.GroupName);
                await LogModerationAsync(userId, updated.GroupId, action, GroupModerationTargetType.Report, updated.Id.ToString(), authorId, gate.Content?.Content, trimmedNotes);
                await NotifyAsync(updated.ReporterId, ReviewedMessage, FeedContentReportService.ReviewedTemplateKey, new { ReportId = updated.Id });

                foreach (var other in closedOthers)
                {
                    await LogReviewAuditAsync(userId, other.Id, updated.GroupId, other.PostId, other.CommentId, status, gate.Content?.GroupName);
                    await LogModerationAsync(userId, updated.GroupId, action, GroupModerationTargetType.Report, other.Id.ToString(), authorId, gate.Content?.Content, trimmedNotes);
                    await NotifyAsync(other.ReporterId, ReviewedMessage, FeedContentReportService.ReviewedTemplateKey, new { ReportId = other.Id });
                }

                return FeedReportResult.Ok(updated.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing group report {ReportId}", reportId);
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
        /// <returns>The report and its content when allowed; otherwise the reason it was refused.</returns>
        private async Task<(GroupContentReport? Report, GroupReportContentSnapshot? Content, FeedReportError Error)> GateAsync(string userId, int reportId, CancellationToken cancellationToken)
        {
            if (!await IsSystemModeratorOrAdminAsync(userId))
            {
                return (null, null, FeedReportError.NotAuthorized);
            }

            GroupContentReport? report;
            GroupReportContentSnapshot? content;
            try
            {
                report = await _repository.GetByIdAsync(reportId, cancellationToken);
                content = report == null ? null : await _repository.GetContentSnapshotAsync(report.PostId, report.CommentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading group report {ReportId}", reportId);
                return (null, null, FeedReportError.Failed);
            }

            if (report == null)
            {
                return (null, null, FeedReportError.ReportNotFound);
            }

            if (report.Status is not (ReportStatus.NotReviewed or ReportStatus.UnderReview))
            {
                return (null, null, FeedReportError.AlreadyReviewed);
            }

            if (content != null && string.Equals(content.AuthorId, userId, StringComparison.Ordinal))
            {
                return (null, null, FeedReportError.OwnContent);
            }

            if (string.Equals(report.ReporterId, userId, StringComparison.Ordinal))
            {
                return (null, null, FeedReportError.OwnReport);
            }

            return (report, content, FeedReportError.None);
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
        /// Audits a report status decision, including the group the report belongs to.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="groupId">The group ID.</param>
        /// <param name="postId">The reported post, or null.</param>
        /// <param name="commentId">The reported comment, or null.</param>
        /// <param name="decision">The decision applied.</param>
        /// <param name="groupName">The group name, recorded for the activity log, when known.</param>
        private Task LogReviewAuditAsync(string userId, int reportId, int groupId, int? postId, int? commentId, ReportStatus decision, string? groupName = null)
            => LogAuditAsync(userId, ActionCategory.SystemModeratorReviewReport, new SystemModeratorReviewReportDetails
            {
                OperationType = OperationTypeEnum.Update.ToString(),
                ReportId = reportId,
                PostId = postId,
                CommentId = commentId,
                GroupId = groupId,
                GroupName = groupName,
                Decision = decision.ToString()
            });

        /// <summary>
        /// Audits the removal of a group post (category 26) or comment (category 27), including the group.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="report">The report that led to the removal.</param>
        /// <param name="content">The removed content as it was before deletion.</param>
        /// <param name="notes">The moderator's notes.</param>
        private Task LogRemovalAuditAsync(string userId, GroupContentReport report, GroupReportContentSnapshot content, string? notes)
        {
            var summary = PlainTextExcerpt.Create(content.Content, SystemModerationService.ExcerptLength);
            if (report.CommentId.HasValue)
            {
                return LogAuditAsync(userId, ActionCategory.SystemModeratorRemovePostComment, new SystemModeratorRemovePostCommentDetails
                {
                    OperationType = OperationTypeEnum.Delete.ToString(),
                    PostId = content.PostId,
                    CommentId = report.CommentId.Value,
                    PostType = GroupPostType,
                    GroupId = content.GroupId,
                    GroupName = content.GroupName,
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
                PostType = GroupPostType,
                GroupId = content.GroupId,
                GroupName = content.GroupName,
                PostAuthorId = content.AuthorId,
                PostAuthorDisplayName = content.AuthorName,
                ContentSummary = summary,
                ViolationReason = report.Reason.ToString(),
                ModeratorNotes = notes
            });
        }

        /// <summary>
        /// Records the action in the group moderation log, marked as a system-level action. The log never throws.
        /// </summary>
        /// <param name="userId">The acting moderator's ID.</param>
        /// <param name="groupId">The group ID.</param>
        /// <param name="action">The action taken.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="targetId">The target entity ID.</param>
        /// <param name="targetUserId">The affected user's ID.</param>
        /// <param name="contentHtml">The content the summary is derived from, or null.</param>
        /// <param name="reason">The moderator's notes.</param>
        private Task LogModerationAsync(string userId, int groupId, GroupModerationAction action, GroupModerationTargetType targetType, string? targetId, string? targetUserId, string? contentHtml, string? reason)
            => _moderationLog.LogAsync(new GroupModerationLogRequest
            {
                GroupId = groupId,
                ActorUserId = userId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                TargetUserId = targetUserId,
                Summary = contentHtml == null ? null : PlainTextExcerpt.Create(contentHtml, SystemModerationService.ExcerptLength),
                Reason = reason,
                IsSystemAction = true
            });

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
