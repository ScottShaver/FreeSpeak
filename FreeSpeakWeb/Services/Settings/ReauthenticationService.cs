using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// The outcome of a re-authentication attempt.
    /// </summary>
    public enum ReauthenticationOutcome
    {
        /// <summary>The password was correct; access is granted for the validity period.</summary>
        Success,

        /// <summary>The password was wrong.</summary>
        InvalidPassword,

        /// <summary>Too many failed attempts; try again later.</summary>
        LockedOut,

        /// <summary>The account has no local password, so re-authentication is not possible.</summary>
        NoPassword,

        /// <summary>The user is not a system administrator or does not exist.</summary>
        NotAuthorized
    }

    /// <summary>
    /// Singleton state of recent administrator re-authentications and failed attempts.
    /// </summary>
    public class ReauthenticationStore
    {
        /// <summary>How long a successful re-authentication stays valid.</summary>
        public static readonly TimeSpan Validity = TimeSpan.FromMinutes(5);

        /// <summary>The number of failed attempts that triggers a lockout.</summary>
        public const int MaxFailedAttempts = 5;

        /// <summary>The window in which failed attempts are counted.</summary>
        public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

        private readonly TimeProvider _timeProvider;
        private readonly object _gate = new();
        private readonly Dictionary<string, DateTimeOffset> _grants = new();
        private readonly Dictionary<string, List<DateTimeOffset>> _failures = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ReauthenticationStore"/> class.
        /// </summary>
        /// <param name="timeProvider">The time provider.</param>
        public ReauthenticationStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Records a successful re-authentication and clears failed attempts.
        /// </summary>
        /// <param name="userId">The user.</param>
        public void Grant(string userId)
        {
            lock (_gate)
            {
                _grants[userId] = _timeProvider.GetUtcNow().Add(Validity);
                _failures.Remove(userId);
            }
        }

        /// <summary>
        /// Determines whether the user has a still-valid re-authentication.
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>True if valid.</returns>
        public bool IsValid(string userId)
        {
            lock (_gate)
            {
                if (_grants.TryGetValue(userId, out var expires) && expires > _timeProvider.GetUtcNow())
                {
                    return true;
                }

                _grants.Remove(userId);
                return false;
            }
        }

        /// <summary>
        /// Records a failed attempt.
        /// </summary>
        /// <param name="userId">The user.</param>
        public void RegisterFailure(string userId)
        {
            lock (_gate)
            {
                Prune(userId).Add(_timeProvider.GetUtcNow());
            }
        }

        /// <summary>
        /// Determines whether the user is locked out because of failed attempts.
        /// </summary>
        /// <param name="userId">The user.</param>
        /// <returns>True if locked out.</returns>
        public bool IsLockedOut(string userId)
        {
            lock (_gate)
            {
                return Prune(userId).Count >= MaxFailedAttempts;
            }
        }

        private List<DateTimeOffset> Prune(string userId)
        {
            if (!_failures.TryGetValue(userId, out var list))
            {
                list = new List<DateTimeOffset>();
                _failures[userId] = list;
            }

            var cutoff = _timeProvider.GetUtcNow() - FailureWindow;
            list.RemoveAll(t => t < cutoff);
            return list;
        }
    }

    /// <summary>
    /// Verifies the administrator's own password before sensitive pages open.
    /// </summary>
    public interface IReauthenticationService
    {
        /// <summary>
        /// Checks the password, rate limits failures and audits every attempt.
        /// </summary>
        /// <param name="userId">The acting administrator.</param>
        /// <param name="password">The password entered.</param>
        /// <returns>The outcome.</returns>
        Task<ReauthenticationOutcome> ReauthenticateAsync(string userId, string password);

        /// <summary>
        /// Determines whether the administrator has a still-valid re-authentication.
        /// </summary>
        /// <param name="userId">The administrator.</param>
        /// <returns>True if valid.</returns>
        bool IsReauthenticated(string userId);
    }

    /// <summary>
    /// Default <see cref="IReauthenticationService"/> based on ASP.NET Core Identity.
    /// </summary>
    public class ReauthenticationService : IReauthenticationService
    {
        private const string SystemAdministratorRole = "SystemAdministrator";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ReauthenticationStore _store;
        private readonly ILogger<ReauthenticationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReauthenticationService"/> class.
        /// </summary>
        /// <param name="userManager">The Identity user manager.</param>
        /// <param name="roleService">The role service.</param>
        /// <param name="auditLogRepository">The audit log repository.</param>
        /// <param name="store">The shared re-authentication state.</param>
        /// <param name="logger">The logger.</param>
        public ReauthenticationService(
            UserManager<ApplicationUser> userManager,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            ReauthenticationStore store,
            ILogger<ReauthenticationService> logger)
        {
            _userManager = userManager;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _store = store;
            _logger = logger;
        }

        /// <inheritdoc/>
        public bool IsReauthenticated(string userId) => _store.IsValid(userId);

        /// <inheritdoc/>
        public async Task<ReauthenticationOutcome> ReauthenticateAsync(string userId, string password)
        {
            if (string.IsNullOrEmpty(userId) || !await _roleService.IsUserInRoleAsync(userId, SystemAdministratorRole))
            {
                return ReauthenticationOutcome.NotAuthorized;
            }

            if (_store.IsLockedOut(userId))
            {
                await AuditAsync(userId, "LockedOut", "Re-authentication refused: too many failed attempts");
                return ReauthenticationOutcome.LockedOut;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ReauthenticationOutcome.NotAuthorized;
            }

            if (!await _userManager.HasPasswordAsync(user))
            {
                return ReauthenticationOutcome.NoPassword;
            }

            if (!string.IsNullOrEmpty(password) && await _userManager.CheckPasswordAsync(user, password))
            {
                _store.Grant(userId);
                await AuditAsync(userId, "Success", "Re-authenticated to view the effective configuration");
                return ReauthenticationOutcome.Success;
            }

            _store.RegisterFailure(userId);
            _logger.LogWarning("Failed re-authentication attempt for system administrator {UserId}", userId);
            await AuditAsync(userId, "Failure", "Re-authentication failed: incorrect password");
            return ReauthenticationOutcome.InvalidPassword;
        }

        private Task AuditAsync(string userId, string outcome, string description) =>
            _auditLogRepository.LogActionAsync(userId, ActionCategory.SystemAdminReauthentication, new SystemAdminReauthenticationDetails
            {
                OperationType = nameof(OperationTypeEnum.Edit),
                Outcome = outcome,
                Description = description
            });
    }
}
