namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user group leave audit log entries.
    /// Tracks when users leave groups voluntarily or are removed.
    /// </summary>
    public class UserGroupLeaveDetails
    {
        /// <summary>
        /// Gets or sets the type of group leave action.
        /// Examples: "Voluntary", "Removed", "Banned", "GroupDeleted".
        /// </summary>
        public string LeaveType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Gets or sets the reason for leaving or removal, if provided.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the admin who removed the user, if applicable.
        /// </summary>
        public string? RemovedByUserId { get; set; }
    }
}
