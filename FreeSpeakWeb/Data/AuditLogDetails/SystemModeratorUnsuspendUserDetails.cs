namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for audit log entries recorded when a system moderator or administrator lifts a suspension.
    /// </summary>
    public class SystemModeratorUnsuspendUserDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the ID of the suspension record that was lifted.
        /// </summary>
        public int SuspensionId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user whose suspension was lifted.
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name of the user at the time the suspension was lifted.
        /// </summary>
        public string? TargetUserName { get; set; }

        /// <summary>
        /// Gets or sets the note the moderator recorded when lifting the suspension.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time the suspension was lifted.
        /// </summary>
        public DateTime LiftedAtUtc { get; set; }
    }
}
