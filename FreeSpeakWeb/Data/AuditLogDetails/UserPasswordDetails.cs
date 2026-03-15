namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user password change audit log entries.
    /// Tracks password changes and reset operations.
    /// </summary>
    public class UserPasswordDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether two-factor authentication was used to verify the change.
        /// </summary>
        public bool TwoFactorVerified { get; set; }

        /// <summary>
        /// Gets or sets the failure reason if the operation was not successful.
        /// </summary>
        public string? FailureReason { get; set; }
    }
}
