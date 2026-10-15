namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for a system administrator re-authentication attempt.
    /// </summary>
    public class SystemAdminReauthenticationDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the outcome: "Success", "Failure" or "LockedOut".
        /// </summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a human-readable description of the attempt.
        /// </summary>
        public string? Description { get; set; }
    }
}
