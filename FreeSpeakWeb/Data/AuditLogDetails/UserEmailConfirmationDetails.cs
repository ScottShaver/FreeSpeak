namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for email confirmation resend audit log entries.
    /// Tracks when users request a new confirmation email.
    /// </summary>
    public class UserEmailConfirmationDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the email address to which the confirmation was sent.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
    }
}
