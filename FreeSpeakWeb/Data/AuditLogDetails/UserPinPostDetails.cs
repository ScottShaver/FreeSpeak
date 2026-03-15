namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user pin/unpin post audit log entries.
    /// Tracks when users pin or unpin posts to their profile.
    /// </summary>
    public class UserPinPostDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the post being pinned/unpinned.
        /// </summary>
        public int PostId { get; set; }
    }
}
