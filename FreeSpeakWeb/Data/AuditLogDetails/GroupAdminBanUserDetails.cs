namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin ban user audit log entries.
    /// Tracks when administrators ban users from groups.
    /// </summary>
    public class GroupAdminBanUserDetails : AuditLogDetailsBase
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
        /// Gets or sets the unique identifier of the banned user.
        /// </summary>
        public string BannedUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the banned user.
        /// </summary>
        public string? BannedUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the reason for the ban.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the ban is permanent.
        /// </summary>
        public bool IsPermanent { get; set; } = true;

        /// <summary>
        /// Gets or sets the duration of the ban in days, if not permanent.
        /// </summary>
        public int? BanDurationDays { get; set; }
    }
}
