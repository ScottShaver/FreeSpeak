namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user personal data operation audit log entries.
    /// Tracks data export requests, data deletion, and privacy settings changes.
    /// </summary>
    public class UserPersonalDataDetails
    {
        /// <summary>
        /// Gets or sets the type of personal data operation performed.
        /// Examples: "DataExport", "DataDeletion", "PrivacySettingsChange", "DataDownload".
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific data types affected by the operation.
        /// Examples: "Profile", "Posts", "Messages", "All".
        /// </summary>
        public string? DataScope { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets any additional details or reason for the operation.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the IP address from which the request was made.
        /// </summary>
        public string? IpAddress { get; set; }
    }
}
