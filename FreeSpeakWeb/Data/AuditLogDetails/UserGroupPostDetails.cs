namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user group post audit log entries.
    /// Tracks when users create, update, or delete posts within groups.
    /// </summary>
    public class UserGroupPostDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the group where the post was created.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group where the post was created.
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the post.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the post contains media attachments.
        /// </summary>
        public bool HasMedia { get; set; }
    }
}
