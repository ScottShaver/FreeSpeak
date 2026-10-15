namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for a changed system setting. Values of secret settings are never recorded.
    /// </summary>
    public class SystemSettingChangedDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the settings group identifier (for example "smtp").
        /// </summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configuration key that changed.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous value, or null for secrets.
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// Gets or sets the new value, or null for secrets.
        /// </summary>
        public string? NewValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the setting is a secret.
        /// </summary>
        public bool IsSecret { get; set; }

        /// <summary>
        /// Gets or sets a human-readable description of the change.
        /// </summary>
        public string? Description { get; set; }
    }
}
