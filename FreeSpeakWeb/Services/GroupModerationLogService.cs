using System.Net;
using System.Text.RegularExpressions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Records and reads the per-group moderation log. Logging never throws into the caller.
    /// </summary>
    public class GroupModerationLogService : IGroupModerationLogService
    {
        private const int MaxPageSize = 100;
        private static readonly Regex HtmlTags = new("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        private readonly IGroupModerationLogRepository _repository;
        private readonly GroupAccessValidator _accessValidator;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IOptions<GroupModerationLogSettings> _settings;
        private readonly ILogger<GroupModerationLogService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupModerationLogService"/> class.
        /// </summary>
        /// <param name="repository">Repository for log entries.</param>
        /// <param name="accessValidator">Validator used for moderator and admin checks.</param>
        /// <param name="auditLogRepository">Global audit log (used to record exports).</param>
        /// <param name="settings">Moderation log settings.</param>
        /// <param name="logger">Logger.</param>
        public GroupModerationLogService(
            IGroupModerationLogRepository repository,
            GroupAccessValidator accessValidator,
            IAuditLogRepository auditLogRepository,
            IOptions<GroupModerationLogSettings> settings,
            ILogger<GroupModerationLogService> logger)
        {
            _repository = repository;
            _accessValidator = accessValidator;
            _auditLogRepository = auditLogRepository;
            _settings = settings;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task LogAsync(GroupModerationLogRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var ids = new List<string> { request.ActorUserId };
                if (!string.IsNullOrEmpty(request.TargetUserId))
                {
                    ids.Add(request.TargetUserId);
                }

                var names = await _repository.GetDisplayNamesAsync(ids, cancellationToken);
                names.TryGetValue(request.ActorUserId, out var actorName);
                string? targetName = null;
                if (!string.IsNullOrEmpty(request.TargetUserId) && names.TryGetValue(request.TargetUserId, out var tn))
                {
                    targetName = tn;
                }

                var entry = new GroupModerationLogEntry
                {
                    GroupId = request.GroupId,
                    ActorUserId = request.ActorUserId,
                    ActorDisplayName = Truncate(actorName ?? request.ActorUserId, GroupModerationLogEntry.MaxDisplayNameLength)!,
                    Action = request.Action,
                    TargetType = request.TargetType,
                    TargetId = Truncate(request.TargetId, GroupModerationLogEntry.MaxUserIdLength),
                    TargetUserId = Truncate(request.TargetUserId, GroupModerationLogEntry.MaxUserIdLength),
                    TargetUserDisplayName = Truncate(targetName, GroupModerationLogEntry.MaxDisplayNameLength),
                    Summary = Truncate(ToPlainText(request.Summary), GroupModerationLogEntry.MaxSummaryLength),
                    Reason = Truncate(request.Reason?.Trim(), GroupModerationLogEntry.MaxReasonLength),
                    CreatedAt = DateTime.UtcNow,
                    IsSystemAction = request.IsSystemAction
                };

                await _repository.AddAsync(entry, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write moderation log entry {Action} for group {GroupId}", request.Action, request.GroupId);
            }
        }

        /// <inheritdoc/>
        public async Task<GroupModerationLogPage> GetLogAsync(
            int groupId,
            string requestingUserId,
            GroupModerationLogFilter filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (!await IsAuthorizedAsync(groupId, requestingUserId))
            {
                return new GroupModerationLogPage(Array.Empty<GroupModerationLogEntryDto>(), 0, false);
            }

            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            page = Math.Max(page, 1);

            var (items, total) = await _repository.GetPagedAsync(groupId, filter, page, pageSize, cancellationToken);
            return new GroupModerationLogPage(items.Select(ToDto).ToList(), total, true);
        }

        /// <inheritdoc/>
        public async Task<GroupModerationLogExportResult> ExportCsvAsync(
            int groupId,
            string requestingUserId,
            GroupModerationLogFilter filter,
            CancellationToken cancellationToken = default)
        {
            if (!await IsAuthorizedAsync(groupId, requestingUserId))
            {
                return new GroupModerationLogExportResult(false, string.Empty, 0, false);
            }

            var cap = Math.Max(_settings.Value.ExportMaxRows, 1);
            // Fetch one extra row to detect truncation.
            var rows = await _repository.GetForExportAsync(groupId, filter, cap + 1, cancellationToken);
            var truncated = rows.Count > cap;
            if (truncated)
            {
                rows = rows.Take(cap).ToList();
            }

            var csv = GroupModerationLogCsv.Build(rows);

            try
            {
                await _auditLogRepository.LogActionAsync(requestingUserId, ActionCategory.GroupModerationLogExport,
                    new GroupModerationLogExportDetails
                    {
                        OperationType = nameof(OperationTypeEnum.Read),
                        GroupId = groupId,
                        RowCount = rows.Count,
                        Truncated = truncated,
                        ExportedAtUtc = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit moderation log export for group {GroupId}", groupId);
            }

            return new GroupModerationLogExportResult(true, csv, rows.Count, truncated);
        }

        /// <summary>
        /// Checks that the user is a group moderator, group admin or system administrator.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>True when authorized.</returns>
        private async Task<bool> IsAuthorizedAsync(int groupId, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            return await _accessValidator.IsGroupAdminOrModeratorAsync(groupId, userId);
        }

        /// <summary>
        /// Maps an entity to its DTO, marking the timestamp as UTC.
        /// </summary>
        /// <param name="e">The entity.</param>
        /// <returns>The DTO.</returns>
        private static GroupModerationLogEntryDto ToDto(GroupModerationLogEntry e) => new(
            e.Id, e.GroupId, e.ActorUserId, e.ActorDisplayName, e.Action, e.TargetType, e.TargetId,
            e.TargetUserId, e.TargetUserDisplayName, e.Summary, e.Reason,
            DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc), e.IsSystemAction);

        /// <summary>
        /// Strips HTML tags and collapses whitespace.
        /// </summary>
        /// <param name="value">Raw text that may contain HTML.</param>
        /// <returns>Plain text, or null.</returns>
        private static string? ToPlainText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = WebUtility.HtmlDecode(HtmlTags.Replace(value, " "));
            return Whitespace.Replace(text, " ").Trim();
        }

        /// <summary>
        /// Truncates a value to a maximum length.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="max">Maximum length.</param>
        /// <returns>The possibly truncated value.</returns>
        private static string? Truncate(string? value, int max) =>
            value != null && value.Length > max ? value[..max] : value;
    }
}
