namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for a system setting reset to its appsettings.json default.
    /// </summary>
    public class SystemSettingResetDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the settings group identifier.
        /// </summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configuration key that was reset.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the setting is a secret.
        /// </summary>
        public bool IsSecret { get; set; }

        /// <summary>
        /// Gets or sets a human-readable description of the reset.
        /// </summary>
        public string? Description { get; set; }
    }
}
