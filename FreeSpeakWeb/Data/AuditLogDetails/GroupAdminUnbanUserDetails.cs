namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group unban user audit log entries.
    /// </summary>
    public class GroupAdminUnbanUserDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the unbanned user.
        /// </summary>
        public string UnbannedUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the unbanned user.
        /// </summary>
        public string? UnbannedUserDisplayName { get; set; }
    }
}
