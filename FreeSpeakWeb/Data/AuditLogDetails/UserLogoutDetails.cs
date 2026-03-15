namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user logout audit log entries.
    /// Tracks user session end events.
    /// </summary>
    public class UserLogoutDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the method of logout.
        /// Examples: "Manual", "SessionTimeout", "TokenExpired", "ForcedByAdmin".
        /// </summary>
        public string LogoutMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration of the session in minutes before logout.
        /// </summary>
        public int? SessionDurationMinutes { get; set; }
    }
}
