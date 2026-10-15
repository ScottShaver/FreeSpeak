namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// A validation problem for one setting, expressed as a localization key.
    /// </summary>
    /// <param name="Key">The configuration key the error belongs to.</param>
    /// <param name="ResourceKey">The localization key of the message.</param>
    /// <param name="Args">Format arguments for the message.</param>
    public sealed record SettingValidationError(string Key, string ResourceKey, params object[] Args);

    /// <summary>
    /// The current state of one field of a settings group. Secret values are never included.
    /// </summary>
    /// <param name="Definition">The field metadata.</param>
    /// <param name="Value">The effective value as shown in the form; always null for secrets.</param>
    /// <param name="IsSet">For secrets, whether a value is configured.</param>
    /// <param name="IsOverridden">Whether a database value overrides the file default.</param>
    public sealed record SettingFieldState(SettingFieldDefinition Definition, string? Value, bool IsSet, bool IsOverridden);

    /// <summary>
    /// The current state of a settings group.
    /// </summary>
    /// <param name="Group">The group metadata.</param>
    /// <param name="Fields">The field states in display order.</param>
    public sealed record SettingGroupState(SettingGroupDefinition Group, IReadOnlyList<SettingFieldState> Fields);

    /// <summary>
    /// The outcome of saving a settings group.
    /// </summary>
    /// <param name="Success">Whether the values were saved.</param>
    /// <param name="Errors">Validation errors when not successful.</param>
    /// <param name="ChangedKeys">The keys that were changed.</param>
    /// <param name="RestartRequired">Whether the group only applies after a restart.</param>
    public sealed record SaveSettingsResult(
        bool Success,
        IReadOnlyList<SettingValidationError> Errors,
        IReadOnlyList<string> ChangedKeys,
        bool RestartRequired);

    /// <summary>
    /// The outcome of a connection test.
    /// </summary>
    /// <param name="Success">Whether the service responded.</param>
    /// <param name="ErrorKey">The localization key of the failure reason, if any.</param>
    public sealed record ConnectionTestResult(bool Success, string? ErrorKey);

    /// <summary>
    /// Reads, validates, saves and resets database-backed system settings. All operations are
    /// restricted to system administrators and re-check the role on the server.
    /// </summary>
    public interface ISystemSettingsService
    {
        /// <summary>
        /// Gets the effective state of a settings group.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="adminUserId">The acting user.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The group state, or null if the group does not exist.</returns>
        /// <exception cref="UnauthorizedAccessException">The user is not a system administrator.</exception>
        Task<SettingGroupState?> GetGroupAsync(string groupId, string adminUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and saves submitted values for a group, reloads configuration and audits each change.
        /// Blank secret values keep the existing secret.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="values">Submitted values keyed by configuration key.</param>
        /// <param name="adminUserId">The acting user.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The result of the save.</returns>
        /// <exception cref="UnauthorizedAccessException">The user is not a system administrator.</exception>
        Task<SaveSettingsResult> SaveGroupAsync(string groupId, IReadOnlyDictionary<string, string?> values, string adminUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all database overrides of a group so file defaults apply again, and audits each reset.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="adminUserId">The acting user.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The number of overrides removed.</returns>
        /// <exception cref="UnauthorizedAccessException">The user is not a system administrator.</exception>
        Task<int> ResetGroupAsync(string groupId, string adminUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pings a ClamAV daemon using the supplied host and port.
        /// </summary>
        /// <param name="host">The ClamAV host.</param>
        /// <param name="port">The ClamAV port.</param>
        /// <param name="adminUserId">The acting user.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The outcome of the ping.</returns>
        /// <exception cref="UnauthorizedAccessException">The user is not a system administrator.</exception>
        Task<ConnectionTestResult> TestVirusScanConnectionAsync(string? host, string? port, string adminUserId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pings a ClamAV daemon. Abstracted so the settings service can be tested without a daemon.
    /// </summary>
    public interface IClamAvPinger
    {
        /// <summary>
        /// Sends a PING to the daemon.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True if the daemon answered PONG.</returns>
        Task<bool> PingAsync(string host, int port, CancellationToken cancellationToken);
    }

    /// <summary>
    /// nClam-based <see cref="IClamAvPinger"/>.
    /// </summary>
    public sealed class NClamPinger : IClamAvPinger
    {
        /// <inheritdoc/>
        public async Task<bool> PingAsync(string host, int port, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var client = new nClam.ClamClient(host, port);
            return await client.PingAsync(timeout.Token);
        }
    }
}
