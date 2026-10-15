using FreeSpeakWeb.Configuration;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// Default <see cref="ISystemSettingsService"/>: stores overrides in the SystemSettings table,
    /// encrypts secrets, reloads configuration and audits every change.
    /// </summary>
    public class SystemSettingsService : ISystemSettingsService
    {
        private const string SystemAdministratorRole = "SystemAdministrator";

        private readonly ISystemSettingsRepository _repository;
        private readonly IConfiguration _configuration;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ISettingsConfigurationReloader _reloader;
        private readonly IDataProtector _protector;
        private readonly IClamAvPinger _pinger;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SystemSettingsService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemSettingsService"/> class.
        /// </summary>
        /// <param name="repository">The settings repository.</param>
        /// <param name="configuration">The application configuration (effective values).</param>
        /// <param name="roleService">The role service used for authorization.</param>
        /// <param name="auditLogRepository">The audit log repository.</param>
        /// <param name="reloader">Reloads the database configuration provider after changes.</param>
        /// <param name="dataProtectionProvider">Provides the secret protector.</param>
        /// <param name="pinger">The ClamAV pinger.</param>
        /// <param name="timeProvider">The time provider.</param>
        /// <param name="logger">The logger.</param>
        public SystemSettingsService(
            ISystemSettingsRepository repository,
            IConfiguration configuration,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            ISettingsConfigurationReloader reloader,
            IDataProtectionProvider dataProtectionProvider,
            IClamAvPinger pinger,
            TimeProvider timeProvider,
            ILogger<SystemSettingsService> logger)
        {
            _repository = repository;
            _configuration = configuration;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _reloader = reloader;
            _protector = dataProtectionProvider.CreateProtector(SystemSettingsProtection.Purpose);
            _pinger = pinger;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<SettingGroupState?> GetGroupAsync(string groupId, string adminUserId, CancellationToken cancellationToken = default)
        {
            await EnsureAdministratorAsync(adminUserId);
            var group = SettingsCatalog.Find(groupId);
            if (group == null)
            {
                return null;
            }

            var rows = (await _repository.GetAllAsync(cancellationToken)).ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
            var fields = group.Fields.Select(f => new SettingFieldState(
                f,
                f.IsSecret ? null : EffectiveDisplayValue(f),
                f.IsSecret && !string.IsNullOrEmpty(_configuration[f.Key]),
                rows.ContainsKey(f.Key))).ToList();
            return new SettingGroupState(group, fields);
        }

        /// <inheritdoc/>
        public async Task<SaveSettingsResult> SaveGroupAsync(
            string groupId,
            IReadOnlyDictionary<string, string?> values,
            string adminUserId,
            CancellationToken cancellationToken = default)
        {
            await EnsureAdministratorAsync(adminUserId);
            var group = SettingsCatalog.Find(groupId)
                ?? throw new ArgumentException("Unknown settings group.", nameof(groupId));

            var errors = SettingsValidator.Validate(group, values, key => !string.IsNullOrEmpty(_configuration[key]));
            if (errors.Count > 0)
            {
                return new SaveSettingsResult(false, errors, Array.Empty<string>(), group.RestartRequired);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var upserts = new List<SystemSetting>();
            var audits = new List<SystemSettingChangedDetails>();

            foreach (var field in group.Fields)
            {
                if (!values.TryGetValue(field.Key, out var raw))
                {
                    continue;
                }

                if (field.IsSecret)
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        continue;
                    }

                    upserts.Add(new SystemSetting
                    {
                        Key = field.Key,
                        Value = _protector.Protect(raw),
                        IsSecret = true,
                        UpdatedAtUtc = now,
                        UpdatedByUserId = adminUserId
                    });
                    audits.Add(CreateChangedDetails(group, field, null, null, "changed"));
                    continue;
                }

                var current = EffectiveDisplayValue(field);
                var submitted = SettingsValidator.Normalize(field, raw);
                if (string.Equals(current, submitted, StringComparison.Ordinal))
                {
                    continue;
                }

                var stored = field.Invert ? (submitted == "true" ? "false" : "true") : submitted;
                upserts.Add(new SystemSetting
                {
                    Key = field.Key,
                    Value = stored,
                    IsSecret = false,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = adminUserId
                });
                audits.Add(CreateChangedDetails(group, field, current, submitted, $"changed from '{current}' to '{submitted}'"));
            }

            if (upserts.Count > 0)
            {
                await _repository.UpsertManyAsync(upserts, cancellationToken);
                _reloader.Reload();
                foreach (var details in audits)
                {
                    await _auditLogRepository.LogActionAsync(adminUserId, ActionCategory.SystemSettingChanged, details);
                }

                _logger.LogInformation("System settings group {Group} updated by {UserId}: {Count} change(s)", group.Id, adminUserId, upserts.Count);
            }

            return new SaveSettingsResult(true, Array.Empty<SettingValidationError>(), upserts.Select(u => u.Key).ToList(), group.RestartRequired);
        }

        /// <inheritdoc/>
        public async Task<int> ResetGroupAsync(string groupId, string adminUserId, CancellationToken cancellationToken = default)
        {
            await EnsureAdministratorAsync(adminUserId);
            var group = SettingsCatalog.Find(groupId)
                ?? throw new ArgumentException("Unknown settings group.", nameof(groupId));

            var existing = (await _repository.GetAllAsync(cancellationToken)).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keys = group.Fields.Where(f => existing.Contains(f.Key)).ToList();
            if (keys.Count == 0)
            {
                return 0;
            }

            var removed = await _repository.DeleteManyAsync(keys.Select(k => k.Key).ToList(), cancellationToken);
            _reloader.Reload();

            foreach (var field in keys)
            {
                await _auditLogRepository.LogActionAsync(adminUserId, ActionCategory.SystemSettingReset, new SystemSettingResetDetails
                {
                    OperationType = nameof(OperationTypeEnum.Remove),
                    Group = group.Id,
                    Key = field.Key,
                    IsSecret = field.IsSecret,
                    Description = $"Setting {field.Key} reset to its default"
                });
            }

            _logger.LogInformation("System settings group {Group} reset by {UserId}: {Count} override(s) removed", group.Id, adminUserId, removed);
            return removed;
        }

        /// <inheritdoc/>
        public async Task<ConnectionTestResult> TestVirusScanConnectionAsync(string? host, string? port, string adminUserId, CancellationToken cancellationToken = default)
        {
            await EnsureAdministratorAsync(adminUserId);

            var trimmedHost = (host ?? string.Empty).Trim();
            if (!SettingsValidator.IsHost(trimmedHost))
            {
                return new ConnectionTestResult(false, "E_Host");
            }

            if (!int.TryParse(port?.Trim(), out var portNumber) || portNumber is < 1 or > 65535)
            {
                return new ConnectionTestResult(false, "E_Range");
            }

            try
            {
                var ok = await _pinger.PingAsync(trimmedHost, portNumber, cancellationToken);
                return ok ? new ConnectionTestResult(true, null) : new ConnectionTestResult(false, "E_PingFailed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "ClamAV connection test to {Host}:{Port} failed", trimmedHost, portNumber);
                return new ConnectionTestResult(false, "E_PingFailed");
            }
        }

        private async Task EnsureAdministratorAsync(string adminUserId)
        {
            if (string.IsNullOrEmpty(adminUserId) || !await _roleService.IsUserInRoleAsync(adminUserId, SystemAdministratorRole))
            {
                throw new UnauthorizedAccessException("Only system administrators can manage system settings.");
            }
        }

        private string EffectiveDisplayValue(SettingFieldDefinition field)
        {
            var raw = _configuration[field.Key];
            var value = SettingsValidator.Normalize(field, string.IsNullOrEmpty(raw) && field.Type != SettingFieldType.Text ? field.Default : raw ?? field.Default);
            if (field.Invert)
            {
                return value == "true" ? "false" : "true";
            }

            return value;
        }

        private static SystemSettingChangedDetails CreateChangedDetails(
            SettingGroupDefinition group,
            SettingFieldDefinition field,
            string? oldValue,
            string? newValue,
            string change) => new()
            {
                OperationType = nameof(OperationTypeEnum.Edit),
                Group = group.Id,
                Key = field.Key,
                OldValue = oldValue,
                NewValue = newValue,
                IsSecret = field.IsSecret,
                Description = $"Setting {field.Key} {change}"
            };
    }
}
