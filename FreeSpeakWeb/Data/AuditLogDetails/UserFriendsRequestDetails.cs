namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user friend request audit log entries.
    /// Tracks when users send, receive, accept, or decline friend requests.
    /// </summary>
    public class UserFriendsRequestDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the user ID of the other user involved in the friend request.
        /// </summary>
        public string? TargetUserId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the other user involved in the friend request.
        /// </summary>
        public string? TargetUserDisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request was initiated by the logged user.
        /// </summary>
        public bool IsInitiator { get; set; }

        /// <summary>
        /// Gets or sets a message included with the friend request, if any.
        /// </summary>
        public string? Message { get; set; }
    }
}
