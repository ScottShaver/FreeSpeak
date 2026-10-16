using System.Text;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Reads and exports the system moderator activity log from the global audit log.
    /// </summary>
    public class SystemModeratorActivityService : ISystemModeratorActivityService
    {
        private const int MaxPageSize = 100;
        private const string SystemModeratorRole = "SystemModerator";
        private const string SystemAdministratorRole = "SystemAdministrator";

        /// <summary>
        /// The audit categories that make up the moderator activity log.
        /// </summary>
        public static readonly ActionCategory[] ActivityCategories =
        {
            ActionCategory.SystemModeratorRemovePost,
            ActionCategory.SystemModeratorRemovePostComment,
            ActionCategory.SystemModeratorReviewReport,
            ActionCategory.SystemModeratorWarnUser,
            ActionCategory.SystemModeratorSuspendUser,
            ActionCategory.SystemModeratorUnsuspendUser
        };

        /// <summary>
        /// The CSV column headers.
        /// </summary>
        public static readonly string[] CsvColumns =
        {
            "Timestamp (UTC)", "Moderator", "Moderator Id", "Action", "Target User", "Target User Id", "Group", "Detail"
        };

        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IGroupReportModerationRepository _userLookup;
        private readonly IRoleService _roleService;
        private readonly IOptions<ModerationSettings> _settings;
        private readonly ILogger<SystemModeratorActivityService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemModeratorActivityService"/> class.
        /// </summary>
        /// <param name="auditLogRepository">The global audit log.</param>
        /// <param name="userLookup">Used to resolve user display names.</param>
        /// <param name="roleService">Used to check system roles.</param>
        /// <param name="settings">Moderation settings (export cap).</param>
        /// <param name="logger">Logger.</param>
        public SystemModeratorActivityService(
            IAuditLogRepository auditLogRepository,
            IGroupReportModerationRepository userLookup,
            IRoleService roleService,
            IOptions<ModerationSettings> settings,
            ILogger<SystemModeratorActivityService> logger)
        {
            _auditLogRepository = auditLogRepository;
            _userLookup = userLookup;
            _roleService = roleService;
            _settings = settings;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ModeratorActivityPage> GetActivityAsync(string requestingUserId, ModeratorActivityFilter filter, int page, int pageSize)
        {
            if (!await IsAuthorizedAsync(requestingUserId))
            {
                return new ModeratorActivityPage(false, Array.Empty<ModeratorActivityEntry>(), 0);
            }

            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            page = Math.Max(page, 1);
            var (rows, total) = await QueryAsync(filter, (page - 1) * pageSize, pageSize);
            return new ModeratorActivityPage(true, await ToEntriesAsync(rows), total);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModeratorOption>> GetModeratorOptionsAsync(string requestingUserId)
        {
            if (!await IsAuthorizedAsync(requestingUserId))
            {
                return Array.Empty<ModeratorOption>();
            }

            var ids = await _auditLogRepository.GetDistinctUserIdsByCategoriesAsync(CategoryNames());
            var options = new List<ModeratorOption>();
            foreach (var id in ids)
            {
                options.Add(new ModeratorOption(id, await NameAsync(id) ?? id));
            }

            return options.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <inheritdoc/>
        public async Task<ModeratorActivityExportResult> ExportCsvAsync(string requestingUserId, ModeratorActivityFilter filter)
        {
            if (!await IsAuthorizedAsync(requestingUserId))
            {
                return new ModeratorActivityExportResult(false, string.Empty, 0, false);
            }

            var cap = Math.Max(_settings.Value.ActivityExportMaxRows, 1);
            var (rows, _) = await QueryAsync(filter, 0, cap + 1);
            var truncated = rows.Count > cap;
            if (truncated)
            {
                rows = rows.Take(cap).ToList();
            }

            var entries = await ToEntriesAsync(rows);
            var csv = BuildCsv(entries);

            try
            {
                await _auditLogRepository.LogActionAsync(requestingUserId, ActionCategory.SystemModeratorActivityExport,
                    new SystemModeratorActivityExportDetails
                    {
                        OperationType = nameof(OperationTypeEnum.Read),
                        RowCount = entries.Count,
                        Truncated = truncated,
                        ExportedAtUtc = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit moderator activity export");
            }

            return new ModeratorActivityExportResult(true, csv, entries.Count, truncated);
        }

        /// <summary>
        /// Builds the CSV text for the entries, neutralizing spreadsheet formulas in every cell.
        /// </summary>
        /// <param name="entries">The entries to write.</param>
        /// <returns>The CSV text.</returns>
        public static string BuildCsv(IEnumerable<ModeratorActivityEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", CsvColumns.Select(GroupModerationLogCsv.Escape))).Append("\r\n");
            foreach (var e in entries)
            {
                var cells = new[]
                {
                    DateTime.SpecifyKind(e.TimestampUtc, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                    e.ModeratorName, e.ModeratorUserId, e.Action.ToString(), e.TargetUserName, e.TargetUserId, e.GroupName, e.Detail
                };
                sb.Append(string.Join(",", cells.Select(GroupModerationLogCsv.Escape))).Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the category names to query for a filter.
        /// </summary>
        /// <returns>All activity category names.</returns>
        private static string[] CategoryNames() => ActivityCategories.Select(c => c.ToString()).ToArray();

        /// <summary>
        /// Runs the filtered query.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="skip">Rows to skip.</param>
        /// <param name="take">Rows to take.</param>
        /// <returns>The rows and the total count.</returns>
        private async Task<(List<AuditLog> Items, int Total)> QueryAsync(ModeratorActivityFilter filter, int skip, int take)
        {
            var categories = filter.Action.HasValue && ActivityCategories.Contains(filter.Action.Value)
                ? new[] { filter.Action.Value.ToString() }
                : CategoryNames();
            DateTime? from = filter.FromDateUtc.HasValue
                ? DateTime.SpecifyKind(filter.FromDateUtc.Value.Date, DateTimeKind.Utc) : null;
            DateTime? to = filter.ToDateUtc.HasValue
                ? DateTime.SpecifyKind(filter.ToDateUtc.Value.Date.AddDays(1), DateTimeKind.Utc) : null;
            return await _auditLogRepository.GetByCategoriesAsync(categories, filter.ModeratorUserId, from, to, skip, take);
        }

        /// <summary>
        /// Converts audit rows to entries, resolving names and reading the category specific details.
        /// </summary>
        /// <param name="rows">The audit rows.</param>
        /// <returns>The entries.</returns>
        private async Task<IReadOnlyList<ModeratorActivityEntry>> ToEntriesAsync(IEnumerable<AuditLog> rows)
        {
            var names = new Dictionary<string, string?>();
            async Task<string?> Name(string? id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (!names.TryGetValue(id, out var n))
                {
                    n = await NameAsync(id);
                    names[id] = n;
                }

                return n;
            }

            var result = new List<ModeratorActivityEntry>();
            foreach (var row in rows)
            {
                if (!Enum.TryParse<ActionCategory>(row.ActionCategory, out var action))
                {
                    continue;
                }

                var (targetId, targetName, detail, groupId, groupName) = ReadDetails(action, row.ActionDetails);
                result.Add(new ModeratorActivityEntry(
                    row.Id,
                    DateTime.SpecifyKind(row.ActionStamp, DateTimeKind.Utc),
                    row.UserId,
                    await Name(row.UserId) ?? row.UserId,
                    action,
                    targetId,
                    targetName ?? await Name(targetId),
                    detail,
                    groupId,
                    groupName));
            }

            return result;
        }

        /// <summary>
        /// Reads the target, detail and group from the JSON details of an audit entry.
        /// </summary>
        /// <param name="action">The action category.</param>
        /// <param name="json">The details JSON.</param>
        /// <returns>The extracted values.</returns>
        private static (string? TargetId, string? TargetName, string? Detail, int? GroupId, string? GroupName) ReadDetails(ActionCategory action, string json)
        {
            switch (action)
            {
                case ActionCategory.SystemModeratorRemovePost:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorRemovePostDetails>(json);
                    return d == null ? default : (d.PostAuthorId, d.PostAuthorDisplayName, Join(d.ViolationReason, d.ContentSummary), d.GroupId, d.GroupName);
                }
                case ActionCategory.SystemModeratorRemovePostComment:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorRemovePostCommentDetails>(json);
                    return d == null ? default : (d.CommentAuthorId, d.CommentAuthorDisplayName, Join(d.ViolationReason, d.ContentSummary), d.GroupId, d.GroupName);
                }
                case ActionCategory.SystemModeratorReviewReport:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorReviewReportDetails>(json);
                    return d == null ? default : (null, null, d.Decision, d.GroupId, null);
                }
                case ActionCategory.SystemModeratorWarnUser:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorWarnUserDetails>(json);
                    return d == null ? default : (d.TargetUserId, d.TargetUserName, d.Reason, null, null);
                }
                case ActionCategory.SystemModeratorSuspendUser:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorSuspendUserDetails>(json);
                    return d == null ? default : (d.TargetUserId, d.TargetUserName, d.Reason, null, null);
                }
                case ActionCategory.SystemModeratorUnsuspendUser:
                {
                    var d = AuditLogDetailsSerializer.FromJson<SystemModeratorUnsuspendUserDetails>(json);
                    return d == null ? default : (d.TargetUserId, d.TargetUserName, d.Note, null, null);
                }
                default:
                    return default;
            }
        }

        /// <summary>
        /// Joins the non-empty parts with a separator.
        /// </summary>
        /// <param name="parts">The parts.</param>
        /// <returns>The joined text, or null when all parts are empty.</returns>
        private static string? Join(params string?[] parts)
        {
            var text = string.Join(" — ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return text.Length == 0 ? null : text;
        }

        /// <summary>
        /// Looks up a display name, returning null on failure.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>The display name or null.</returns>
        private async Task<string?> NameAsync(string userId)
        {
            try
            {
                return await _userLookup.GetUserDisplayNameAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve display name for {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Checks that the user is a system moderator or administrator.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>True when authorized.</returns>
        private async Task<bool> IsAuthorizedAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            return await _roleService.IsUserInRoleAsync(userId, SystemModeratorRole)
                || await _roleService.IsUserInRoleAsync(userId, SystemAdministratorRole);
        }
    }
}
