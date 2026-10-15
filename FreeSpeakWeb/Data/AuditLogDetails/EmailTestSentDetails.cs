namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for a test email sent by a system administrator. Never contains SMTP credentials.
    /// </summary>
    public class EmailTestSentDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the recipient address of the test email.
        /// </summary>
        public string Recipient { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the test email was sent successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the sanitized failure reason key, or null on success.
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC timestamp of the attempt.
        /// </summary>
        public DateTime SentAtUtc { get; set; }
    }
}
