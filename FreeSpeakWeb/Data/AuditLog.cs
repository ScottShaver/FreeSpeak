using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents an audit log entry for tracking user actions and system events.
    /// This table is partitioned by month to efficiently handle large volumes of historical data.
    /// </summary>
    [Table("AuditLogs")]
    public class AuditLog
    {
        /// <summary>
        /// Gets or sets the unique identifier for this audit log entry.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the user identifier associated with this action.
        /// This is a string to match ASP.NET Identity's user ID type but does not use a foreign key
        /// to avoid constraints on historical data retention.
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when this action occurred.
        /// This field is used for partitioning the table by month.
        /// </summary>
        [Required]
        public DateTime ActionStamp { get; set; }

        /// <summary>
        /// Gets or sets the primary category of the action being logged.
        /// Examples: "Authentication", "ProfileUpdate", "ContentCreation", "RoleChange", etc.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ActionCategory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed information about the action in JSON format.
        /// This allows flexible storage of action-specific data without schema changes.
        /// </summary>
        [Required]
        [Column(TypeName = "TEXT")]
        public string ActionDetails { get; set; } = string.Empty;
    }
}
