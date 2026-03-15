namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user group join request audit log entries.
    /// Tracks when users join groups or submit join requests.
    /// </summary>
    public class UserGroupRequestsDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the group requires approval to join.
        /// </summary>
        public bool RequiresApproval { get; set; }

        /// <summary>
        /// Gets or sets a message included with the join request, if any.
        /// </summary>
        public string? RequestMessage { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the admin who approved or denied the request, if applicable.
        /// </summary>
        public string? ProcessedByUserId { get; set; }
    }
}
