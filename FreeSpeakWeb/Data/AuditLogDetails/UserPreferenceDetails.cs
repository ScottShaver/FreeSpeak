namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user preference change audit log entries.
    /// Tracks changes to user settings such as theme, notifications, and other preferences.
    /// </summary>
    public class UserPreferenceDetails
    {
        /// <summary>
        /// Gets or sets the category of preference that was changed.
        /// Examples: "Theme", "Notifications", "Privacy", "Language", "Display".
        /// </summary>
        public string PreferenceCategory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific setting name that was changed.
        /// </summary>
        public string SettingName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous value of the setting.
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// Gets or sets the new value of the setting.
        /// </summary>
        public string? NewValue { get; set; }
    }
}
