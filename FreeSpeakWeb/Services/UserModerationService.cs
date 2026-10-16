using System.Globalization;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Warnings and suspensions issued by system moderators and administrators. Role checks, the moderator
    /// duration limit and the rules about who may be moderated are enforced here, not in the UI.
    /// </summary>
    public class UserModerationService : IUserModerationService
    {
        /// <summary>The notification template key for a warning.</summary>
        public const string WarnedTemplateKey = "UserWarned";

        /// <summary>The notification template key for a temporary suspension.</summary>
        public const string SuspendedTemplateKey = "UserSuspended";

        /// <summary>The notification template key for a permanent suspension.</summary>
        public const string SuspendedPermanentTemplateKey = "UserSuspendedPermanent";

        /// <summary>The notification template key for a lifted suspension.</summary>
        public const string SuspensionLiftedTemplateKey = "UserSuspensionLifted";

        /// <summary>The maximum length of a reason or note.</summary>
        public const int MaxTextLength = 1000;

        /// <summary>The minimum length of a user lookup term.</summary>
        public const int MinSearchLength = 2;

        /// <summary>The longest temporary suspension in days (about 99 years); anything longer must be permanent.</summary>
        public const int MaxDurationDays = 36000;

        private const int SearchLimit = 20;
        private const int HistoryLimit = 50;
        private const string SystemAdministratorRole = "SystemAdministrator";
        private const string SystemModeratorRole = "SystemModerator";

        private readonly IUserModerationRepository _repository;
        private readonly IRoleService _roleService;
        private readonly IUserLockoutService _lockoutService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly NotificationService _notificationService;
        private readonly IOptionsMonitor<ModerationSettings> _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UserModerationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserModerationService"/> class.
        /// </summary>
        /// <param name="repository">Repository for warnings, suspensions and user lookup.</param>
        /// <param name="roleService">Service used to check system roles.</param>
        /// <param name="lockoutService">Service that applies and removes the sign-in lockout.</param>
        /// <param name="auditLogRepository">Audit log for warnings and suspensions.</param>
        /// <param name="notificationService">Service used to notify users.</param>
        /// <param name="settings">Live moderation settings (moderator suspension limit).</param>
        /// <param name="timeProvider">Clock used for server-set timestamps.</param>
        /// <param name="logger">Logger.</param>
        public UserModerationService(
            IUserModerationRepository repository,
            IRoleService roleService,
            IUserLockoutService lockoutService,
            IAuditLogRepository auditLogRepository,
            NotificationService notificationService,
            IOptionsMonitor<ModerationSettings> settings,
            TimeProvider timeProvider,
            ILogger<UserModerationService> logger)
        {
            _repository = repository;
            _roleService = roleService;
            _lockoutService = lockoutService;
            _auditLogRepository = auditLogRepository;
            _notificationService = notificationService;
            _settings = settings;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModerationUserSummary>> SearchUsersAsync(string actorId, string term, CancellationToken cancellationToken = default)
        {
            if (!await IsModeratorOrAdminAsync(actorId) || string.IsNullOrWhiteSpace(term) || term.Trim().Length < MinSearchLength)
            {
                return Array.Empty<ModerationUserSummary>();
            }

            return await _repository.SearchUsersAsync(term, SearchLimit, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UserModerationHistory?> GetUserHistoryAsync(string actorId, string userId, CancellationToken cancellationToken = default)
        {
            if (!await IsModeratorOrAdminAsync(actorId))
            {
                return null;
            }

            var user = await _repository.GetUserAsync(userId, cancellationToken);
            if (user == null)
            {
                return null;
            }

            var isAdmin = await IsAdminAsync(actorId);
            var active = await FindActiveSuspensionAsync(userId, cancellationToken);
            var suspensions = (await _repository.GetSuspensionsAsync(userId, HistoryLimit, cancellationToken))
                .Select(s =>
                {
                    var isActive = active != null && active.Id == s.Id;
                    return s with { IsActive = isActive, CanLift = isActive && (isAdmin || s.SuspendedByUserId == actorId) };
                })
                .ToList();
            var warnings = await _repository.GetWarningsAsync(userId, HistoryLimit, cancellationToken);
            var reports = await _repository.GetReportsAgainstUserAsync(userId, HistoryLimit, cancellationToken);
            var canModerate = await CheckTargetAsync(actorId, userId, isAdmin) == UserModerationError.None;

            return new UserModerationHistory(user, warnings, suspensions, reports, active != null, canModerate, isAdmin, MaxModeratorDays());
        }

        /// <inheritdoc/>
        public async Task<UserModerationResult> WarnUserAsync(string actorId, string targetUserId, string reason, int? relatedReportId = null, CancellationToken cancellationToken = default)
        {
            var gate = await GateAsync(actorId, targetUserId, reason);
            if (gate.Error != UserModerationError.None)
            {
                return UserModerationResult.Fail(gate.Error);
            }

            try
            {
                var now = Now();
                var warning = await _repository.AddWarningAsync(new UserWarning
                {
                    UserId = targetUserId,
                    IssuedByUserId = actorId,
                    Reason = gate.Reason!,
                    RelatedReportId = relatedReportId,
                    CreatedAt = now
                }, cancellationToken);

                await LogAuditAsync(actorId, ActionCategory.SystemModeratorWarnUser, new SystemModeratorWarnUserDetails
                {
                    OperationType = OperationTypeEnum.Create.ToString(),
                    WarningId = warning.Id,
                    TargetUserId = targetUserId,
                    TargetUserName = gate.Target!.UserName,
                    Reason = warning.Reason,
                    RelatedReportId = relatedReportId,
                    IssuedAtUtc = now
                });
                await NotifyAsync(targetUserId, "You received a warning from the moderation team.", WarnedTemplateKey, new { Reason = warning.Reason });

                _logger.LogInformation("User {TargetUserId} warned by {ActorId}", targetUserId, actorId);
                return UserModerationResult.Ok(warning.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warning user {TargetUserId}", targetUserId);
                return UserModerationResult.Fail(UserModerationError.Failed);
            }
        }

        /// <inheritdoc/>
        public async Task<UserModerationResult> SuspendUserAsync(string actorId, string targetUserId, string reason, TimeSpan? duration, int? relatedReportId = null, CancellationToken cancellationToken = default)
        {
            var gate = await GateAsync(actorId, targetUserId, reason);
            if (gate.Error != UserModerationError.None)
            {
                return UserModerationResult.Fail(gate.Error);
            }

            if (duration.HasValue && (duration.Value <= TimeSpan.Zero || duration.Value > TimeSpan.FromDays(MaxDurationDays)))
            {
                return UserModerationResult.Fail(UserModerationError.InvalidDuration);
            }

            if (!gate.IsAdmin)
            {
                if (!duration.HasValue)
                {
                    return UserModerationResult.Fail(UserModerationError.PermanentNotAllowed);
                }

                if (duration.Value > TimeSpan.FromDays(MaxModeratorDays()))
                {
                    return UserModerationResult.Fail(UserModerationError.DurationExceedsLimit);
                }
            }

            try
            {
                if (await FindActiveSuspensionAsync(targetUserId, cancellationToken) != null)
                {
                    return UserModerationResult.Fail(UserModerationError.AlreadySuspended);
                }

                var now = Now();
                DateTime? endsAt = duration.HasValue ? now.Add(duration.Value) : null;
                var suspension = await _repository.AddSuspensionAsync(new UserSuspension
                {
                    UserId = targetUserId,
                    SuspendedByUserId = actorId,
                    Reason = gate.Reason!,
                    RelatedReportId = relatedReportId,
                    CreatedAt = now,
                    EndsAt = endsAt
                }, cancellationToken);

                var locked = endsAt.HasValue
                    ? await _lockoutService.SetLockoutEndAsync(targetUserId, new DateTimeOffset(DateTime.SpecifyKind(endsAt.Value, DateTimeKind.Utc)))
                    : await _lockoutService.SetPermanentLockoutAsync(targetUserId);
                if (!locked)
                {
                    await _repository.DeleteSuspensionAsync(suspension.Id, cancellationToken);
                    return UserModerationResult.Fail(UserModerationError.Failed);
                }

                await LogAuditAsync(actorId, ActionCategory.SystemModeratorSuspendUser, new SystemModeratorSuspendUserDetails
                {
                    OperationType = OperationTypeEnum.Create.ToString(),
                    SuspensionId = suspension.Id,
                    TargetUserId = targetUserId,
                    TargetUserName = gate.Target!.UserName,
                    Reason = suspension.Reason,
                    IsPermanent = !endsAt.HasValue,
                    DurationDays = duration?.TotalDays,
                    StartedAtUtc = now,
                    EndsAtUtc = endsAt,
                    RelatedReportId = relatedReportId
                });

                if (endsAt.HasValue)
                {
                    await NotifyAsync(targetUserId, "Your account was suspended.", SuspendedTemplateKey,
                        new { Reason = suspension.Reason, Until = endsAt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC" });
                }
                else
                {
                    await NotifyAsync(targetUserId, "Your account was suspended.", SuspendedPermanentTemplateKey, new { Reason = suspension.Reason });
                }

                _logger.LogWarning("User {TargetUserId} suspended by {ActorId} until {EndsAt}", targetUserId, actorId, endsAt);
                return UserModerationResult.Ok(suspension.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suspending user {TargetUserId}", targetUserId);
                return UserModerationResult.Fail(UserModerationError.Failed);
            }
        }

        /// <inheritdoc/>
        public async Task<UserModerationResult> LiftSuspensionAsync(string actorId, int suspensionId, string? note, CancellationToken cancellationToken = default)
        {
            if (!await IsModeratorOrAdminAsync(actorId))
            {
                return UserModerationResult.Fail(UserModerationError.NotAuthorized);
            }

            var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (trimmedNote is { Length: > MaxTextLength })
            {
                return UserModerationResult.Fail(UserModerationError.ReasonTooLong);
            }

            try
            {
                var suspension = await _repository.GetSuspensionAsync(suspensionId, cancellationToken);
                if (suspension == null)
                {
                    return UserModerationResult.Fail(UserModerationError.NotFound);
                }

                var active = await FindActiveSuspensionAsync(suspension.UserId, cancellationToken);
                if (active == null || active.Id != suspension.Id)
                {
                    return UserModerationResult.Fail(UserModerationError.NotActive);
                }

                if (!await IsAdminAsync(actorId) && !string.Equals(suspension.SuspendedByUserId, actorId, StringComparison.Ordinal))
                {
                    return UserModerationResult.Fail(UserModerationError.CannotLift);
                }

                var now = Now();
                if (!await _repository.MarkSuspensionLiftedAsync(suspensionId, actorId, now, trimmedNote, cancellationToken))
                {
                    return UserModerationResult.Fail(UserModerationError.NotActive);
                }

                if (!await _lockoutService.RemoveLockoutAsync(suspension.UserId))
                {
                    _logger.LogError("Suspension {SuspensionId} was marked lifted but the lockout of user {UserId} could not be removed", suspensionId, suspension.UserId);
                    return UserModerationResult.Fail(UserModerationError.Failed);
                }

                var target = await _repository.GetUserAsync(suspension.UserId, cancellationToken);
                await LogAuditAsync(actorId, ActionCategory.SystemModeratorUnsuspendUser, new SystemModeratorUnsuspendUserDetails
                {
                    OperationType = OperationTypeEnum.Update.ToString(),
                    SuspensionId = suspensionId,
                    TargetUserId = suspension.UserId,
                    TargetUserName = target?.UserName,
                    Note = trimmedNote,
                    LiftedAtUtc = now
                });
                await NotifyAsync(suspension.UserId, "Your account suspension was lifted.", SuspensionLiftedTemplateKey, new { });

                _logger.LogInformation("Suspension {SuspensionId} lifted by {ActorId}", suspensionId, actorId);
                return UserModerationResult.Ok(suspensionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error lifting suspension {SuspensionId}", suspensionId);
                return UserModerationResult.Fail(UserModerationError.Failed);
            }
        }

        /// <inheritdoc/>
        public async Task<ActiveSuspensionInfo?> GetActiveSuspensionAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var active = await FindActiveSuspensionAsync(userId, cancellationToken);
            return active == null ? null : new ActiveSuspensionInfo(active.Reason, active.EndsAt);
        }

        /// <inheritdoc/>
        public async Task<bool> AcknowledgeWarningAsync(string userId, int warningId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return await _repository.AcknowledgeWarningAsync(warningId, userId, Now(), cancellationToken);
        }

        /// <summary>
        /// Finds the suspension that currently blocks a user: a suspension record that was neither lifted nor
        /// ended, while the user is still locked out. A record is ignored once the lockout was removed elsewhere
        /// (for example by an administrator through the lockout management dialog).
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The active suspension, or null.</returns>
        private async Task<UserSuspension?> FindActiveSuspensionAsync(string userId, CancellationToken cancellationToken)
        {
            var active = await _repository.GetActiveSuspensionAsync(userId, Now(), cancellationToken);
            if (active == null)
            {
                return null;
            }

            var status = await _lockoutService.GetLockoutStatusAsync(userId);
            return status.LockoutEnd.HasValue && status.LockoutEnd.Value > _timeProvider.GetUtcNow() ? active : null;
        }

        /// <summary>
        /// Runs the checks shared by warn and suspend: the caller's role, the reason, and whether the target may be moderated.
        /// </summary>
        /// <param name="actorId">The acting user's ID.</param>
        /// <param name="targetUserId">The target user's ID.</param>
        /// <param name="reason">The raw reason.</param>
        /// <returns>The trimmed reason and target when allowed; otherwise the error.</returns>
        private async Task<(UserModerationError Error, string? Reason, ModerationUserSummary? Target, bool IsAdmin)> GateAsync(string actorId, string targetUserId, string reason)
        {
            if (!await IsModeratorOrAdminAsync(actorId))
            {
                return (UserModerationError.NotAuthorized, null, null, false);
            }

            var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (trimmed == null)
            {
                return (UserModerationError.ReasonRequired, null, null, false);
            }

            if (trimmed.Length > MaxTextLength)
            {
                return (UserModerationError.ReasonTooLong, null, null, false);
            }

            var isAdmin = await IsAdminAsync(actorId);
            var error = await CheckTargetAsync(actorId, targetUserId, isAdmin);
            if (error != UserModerationError.None)
            {
                return (error, null, null, isAdmin);
            }

            var target = await _repository.GetUserAsync(targetUserId);
            return target == null
                ? (UserModerationError.UserNotFound, null, null, isAdmin)
                : (UserModerationError.None, trimmed, target, isAdmin);
        }

        /// <summary>
        /// Checks who may be moderated: never the caller, never an administrator, and a system moderator only by an administrator.
        /// </summary>
        /// <param name="actorId">The acting user's ID.</param>
        /// <param name="targetUserId">The target user's ID.</param>
        /// <param name="actorIsAdmin">Whether the caller is a system administrator.</param>
        /// <returns><see cref="UserModerationError.None"/> when allowed; otherwise the reason.</returns>
        private async Task<UserModerationError> CheckTargetAsync(string actorId, string targetUserId, bool actorIsAdmin)
        {
            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                return UserModerationError.UserNotFound;
            }

            if (string.Equals(actorId, targetUserId, StringComparison.Ordinal))
            {
                return UserModerationError.CannotModerateSelf;
            }

            if (await IsAdminAsync(targetUserId))
            {
                return UserModerationError.CannotModerateStaff;
            }

            if (!actorIsAdmin && await _roleService.IsUserInRoleAsync(targetUserId, SystemModeratorRole))
            {
                return UserModerationError.CannotModerateStaff;
            }

            return UserModerationError.None;
        }

        /// <summary>
        /// Gets the longest suspension, in days, a system moderator may issue (never less than one).
        /// </summary>
        /// <returns>The limit in days.</returns>
        private int MaxModeratorDays() => Math.Max(1, _settings.CurrentValue.MaxModeratorSuspensionDays);

        /// <summary>
        /// Gets the current UTC time from the injected clock.
        /// </summary>
        /// <returns>The current UTC time.</returns>
        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Determines whether the user is a system administrator.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>True for a system administrator.</returns>
        private Task<bool> IsAdminAsync(string userId) => _roleService.IsUserInRoleAsync(userId, SystemAdministratorRole);

        /// <summary>
        /// Determines whether the user holds the SystemModerator or SystemAdministrator role.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>True when the user may use the moderator tools.</returns>
        private async Task<bool> IsModeratorOrAdminAsync(string userId)
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
        /// Sends a neutral in-app notification. Failures are logged and never fail the operation.
        /// </summary>
        /// <param name="userId">The recipient.</param>
        /// <param name="message">The English fallback message.</param>
        /// <param name="templateKey">The localized template key.</param>
        /// <param name="data">Template parameters.</param>
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
