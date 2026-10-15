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

        /// <summary>
        /// Gets or sets the unique identifier of the user who performed the role change.
        /// </summary>
        public string? ActorUserId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the user who performed the role change.
        /// </summary>
        public string? ActorUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the change was refused (for example, removing the last system administrator).
        /// </summary>
        public bool Blocked { get; set; }

        /// <summary>
        /// Gets or sets a human-readable description of the change, such as which role was added or removed and by whom.
        /// </summary>
        public string? Description { get; set; }
    }
}
