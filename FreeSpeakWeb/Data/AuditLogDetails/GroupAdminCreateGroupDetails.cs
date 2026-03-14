namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin create group audit log entries.
    /// Tracks when administrators create new groups with initial settings.
    /// </summary>
    public class GroupAdminCreateGroupDetails
    {
        /// <summary>
        /// Gets or sets the unique identifier of the created group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the created group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a brief description of the group.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the group is public.
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the group is hidden from searches.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the group requires approval to join.
        /// </summary>
        public bool RequiresJoinApproval { get; set; }
    }
}
