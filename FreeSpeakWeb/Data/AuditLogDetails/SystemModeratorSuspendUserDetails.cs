namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for audit log entries recorded when a system moderator or administrator suspends a user.
    /// </summary>
    public class SystemModeratorSuspendUserDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the ID of the suspension record.
        /// </summary>
        public int SuspensionId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the suspended user.
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name of the suspended user at the time of the suspension.
        /// </summary>
        public string? TargetUserName { get; set; }

        /// <summary>
        /// Gets or sets the reason given to the user.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the suspension has no end date.
        /// </summary>
        public bool IsPermanent { get; set; }

        /// <summary>
        /// Gets or sets the suspension length in whole days, or null for a permanent suspension.
        /// </summary>
        public double? DurationDays { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time the suspension started.
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the suspension ends, or null for a permanent suspension.
        /// </summary>
        public DateTime? EndsAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the related feed content report ID, if the suspension came from a report.
        /// </summary>
        public int? RelatedReportId { get; set; }
    }
}
