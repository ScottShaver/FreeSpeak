namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin approve join request audit log entries.
    /// Tracks when administrators approve user join requests.
    /// </summary>
    public class GroupAdminApproveJoinRequestDetails : AuditLogDetailsBase
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
        /// Gets or sets the unique identifier of the approved user.
        /// </summary>
        public string ApprovedUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name of the approved user.
        /// </summary>
        public string? ApprovedUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the date when the original request was submitted.
        /// </summary>
        public DateTime? RequestedAt { get; set; }
    }
}
