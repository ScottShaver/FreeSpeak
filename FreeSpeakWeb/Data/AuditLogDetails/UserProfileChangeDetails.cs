namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user profile change audit log entries.
    /// Tracks modifications to user profile information such as bio, display name, and location.
    /// </summary>
    public class UserProfileChangeDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the name of the profile field that was changed.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous value of the field before the change.
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// Gets or sets the new value of the field after the change.
        /// </summary>
        public string? NewValue { get; set; }

        /// <summary>
        /// Gets or sets any additional notes about the profile change.
        /// </summary>
        public string? Notes { get; set; }
    }
}
