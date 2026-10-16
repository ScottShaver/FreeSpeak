namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for audit log entries recorded when a system moderator or administrator warns a user.
    /// </summary>
    public class SystemModeratorWarnUserDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the ID of the warning record.
        /// </summary>
        public int WarningId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the warned user.
        /// </summary>
        public string TargetUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user name of the warned user at the time of the warning.
        /// </summary>
        public string? TargetUserName { get; set; }

        /// <summary>
        /// Gets or sets the reason given to the user.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the related feed content report ID, if the warning came from a report.
        /// </summary>
        public int? RelatedReportId { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time the warning was issued.
        /// </summary>
        public DateTime IssuedAtUtc { get; set; }
    }
}
