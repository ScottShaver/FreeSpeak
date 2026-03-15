namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for group admin edit group audit log entries.
    /// Tracks modifications to group settings by administrators.
    /// </summary>
    public class GroupAdminEditGroupDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the edited group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of fields that were changed.
        /// </summary>
        public List<string> ChangedFields { get; set; } = new();

        /// <summary>
        /// Gets or sets the previous name of the group, if changed.
        /// </summary>
        public string? OldName { get; set; }

        /// <summary>
        /// Gets or sets the new name of the group, if changed.
        /// </summary>
        public string? NewName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the previous public visibility status, if changed.
        /// </summary>
        public bool? OldIsPublic { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the new public visibility status, if changed.
        /// </summary>
        public bool? NewIsPublic { get; set; }

        /// <summary>
        /// Gets or sets any additional notes about the edit.
        /// </summary>
        public string? Notes { get; set; }
    }
}
