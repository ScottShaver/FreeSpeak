using FreeSpeakWeb.Services.Abstractions;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// One read-only entry of the effective configuration page.
    /// </summary>
    /// <param name="Key">The configuration key.</param>
    /// <param name="Value">The value to display; sensitive parts are masked.</param>
    /// <param name="Masked">Whether any part of the value was masked.</param>
    public sealed record EffectiveConfigurationEntry(string Key, string Value, bool Masked);

    /// <summary>
    /// Provides the read-only, masked view of configuration that cannot be edited in the UI.
    /// </summary>
    public interface IEffectiveConfigurationService
    {
        /// <summary>
        /// Gets the non-editable configuration entries with secrets masked.
        /// </summary>
        /// <param name="adminUserId">The acting administrator, who must have re-authenticated recently.</param>
        /// <returns>The entries.</returns>
        /// <exception cref="UnauthorizedAccessException">Not a system administrator or not re-authenticated.</exception>
        Task<IReadOnlyList<EffectiveConfigurationEntry>> GetEntriesAsync(string adminUserId);
    }

    /// <summary>
    /// Default <see cref="IEffectiveConfigurationService"/>.
    /// </summary>
    public class EffectiveConfigurationService : IEffectiveConfigurationService
    {
        /// <summary>The text shown instead of sensitive values.</summary>
        public const string Mask = "********";

        private static readonly string[] SensitiveConnectionParts =
            { "password", "pwd", "username", "user id", "uid", "user", "secret", "token", "accountkey" };

        private static readonly string[] SensitiveKeyParts = { "password", "secret", "token", "pwd" };

        private readonly IConfiguration _configuration;
        private readonly IRoleService _roleService;
        private readonly IReauthenticationService _reauthentication;

        /// <summary>
        /// Initializes a new instance of the <see cref="EffectiveConfigurationService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="roleService">The role service.</param>
        /// <param name="reauthentication">The re-authentication service.</param>
        public EffectiveConfigurationService(IConfiguration configuration, IRoleService roleService, IReauthenticationService reauthentication)
        {
            _configuration = configuration;
            _roleService = roleService;
            _reauthentication = reauthentication;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<EffectiveConfigurationEntry>> GetEntriesAsync(string adminUserId)
        {
            if (string.IsNullOrEmpty(adminUserId)
                || !await _roleService.IsUserInRoleAsync(adminUserId, "SystemAdministrator")
                || !_reauthentication.IsReauthenticated(adminUserId))
            {
                throw new UnauthorizedAccessException("Password re-authentication by a system administrator is required.");
            }

            var entries = new List<EffectiveConfigurationEntry>();
            foreach (var section in new[] { "ConnectionStrings", "SystemAdministratorInitInfo" })
            {
                foreach (var child in _configuration.GetSection(section).GetChildren().OrderBy(c => c.Key, StringComparer.Ordinal))
                {
                    entries.Add(MaskEntry($"{section}:{child.Key}", child.Value ?? string.Empty));
                }
            }

            entries.Add(MaskEntry("AllowedHosts", _configuration["AllowedHosts"] ?? string.Empty));
            return entries;
        }

        /// <summary>
        /// Masks sensitive parts of a configuration value. Connection strings keep host-style parts but
        /// hide passwords and user names; keys that look secret are masked entirely.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The raw value.</param>
        /// <returns>The entry to display.</returns>
        public static EffectiveConfigurationEntry MaskEntry(string key, string value)
        {
            if (value.Length == 0)
            {
                return new EffectiveConfigurationEntry(key, value, false);
            }

            if (SensitiveKeyParts.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return new EffectiveConfigurationEntry(key, Mask, true);
            }

            if (key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
            {
                return MaskConnectionString(key, value);
            }

            return new EffectiveConfigurationEntry(key, value, false);
        }

        private static EffectiveConfigurationEntry MaskConnectionString(string key, string value)
        {
            var masked = false;
            var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                var index = part.IndexOf('=');
                if (index < 0)
                {
                    masked = true;
                    return Mask;
                }

                var name = part[..index].Trim();
                if (SensitiveConnectionParts.Any(p => name.Equals(p, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("password", StringComparison.OrdinalIgnoreCase)))
                {
                    masked = true;
                    return $"{name}={Mask}";
                }

                return part;
            });

            return new EffectiveConfigurationEntry(key, string.Join(';', parts), masked);
        }
    }
}
