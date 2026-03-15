namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin close group audit log entries.
    /// Tracks when administrators close or delete groups.
    /// </summary>
    public class GroupAdminCloseGroupDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the closed group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the closed group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for closing the group.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the type of closure action.
        /// Examples: "Deleted", "Archived", "Suspended".
        /// </summary>
        public string CloseType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the member count at the time of closure.
        /// </summary>
        public int MemberCountAtClosure { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether members were notified.
        /// </summary>
        public bool MembersNotified { get; set; }
    }
}
