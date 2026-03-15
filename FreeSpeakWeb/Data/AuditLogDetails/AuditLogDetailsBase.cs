namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Base class for all audit log detail classes.
    /// Provides common properties used across different audit log entry types.
    /// </summary>
    public abstract class AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the type of operation performed.
        /// Should be set to the string representation of an OperationType enum value.
        /// </summary>
        public string? OperationType { get; set; }

        /// <summary>
        /// Gets or sets the IP address from which the operation was performed.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Gets or sets the user agent string of the browser or client used.
        /// </summary>
        public string? UserAgent { get; set; }

    }
}
