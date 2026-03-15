namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin deny join request audit log entries.
    /// Tracks when administrators deny user join requests.
    /// </summary>
    public class GroupAdminDenyJoinRequestDetails : AuditLogDetailsBase
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
        /// Gets or sets the unique identifier of the join request.
        /// </summary>
        public int JoinRequestId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the denied user.
        /// </summary>
        public string DeniedUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the denied user.
        /// </summary>
        public string? DeniedUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the reason for denying the request.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the date when the original request was submitted.
        /// </summary>
        public DateTime? RequestedAt { get; set; }
    }
}
