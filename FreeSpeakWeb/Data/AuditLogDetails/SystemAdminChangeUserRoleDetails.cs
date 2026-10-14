namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for system role change audit log entries.
    /// The acting user is stored as the audit log's user; this class records the target and role.
    /// </summary>
    public class SystemAdminChangeUserRoleDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user whose role changed.
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the user whose role changed.
        /// </summary>
        public string? TargetUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the name of the role that was added or removed.
        /// </summary>
        public string RoleName { get; set; } = string.Empty;
    }
}
