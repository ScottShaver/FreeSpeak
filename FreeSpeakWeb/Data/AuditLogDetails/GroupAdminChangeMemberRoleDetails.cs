namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin change member role audit log entries.
    /// Tracks when administrators change member roles within groups.
    /// </summary>
    public class GroupAdminChangeMemberRoleDetails : AuditLogDetailsBase
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
        /// Gets or sets the unique identifier of the target user.
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the target user.
        /// </summary>
        public string? TargetUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the previous role of the user.
        /// Examples: "Member", "Moderator", "Admin".
        /// </summary>
        public string OldRole { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new role of the user.
        /// Examples: "Member", "Moderator", "Admin".
        /// </summary>
        public string NewRole { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for the role change, if provided.
        /// </summary>
        public string? Reason { get; set; }
    }
}
