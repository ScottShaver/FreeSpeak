namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a notification sent to a user
    /// </summary>
    public class UserNotification
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the user this notification is for
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Navigation property to the user
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// The type of notification
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// The notification message
        /// </summary>
        public required string Message { get; set; }

        /// <summary>
        /// Additional JSON data for the notification (e.g., post ID, user ID, etc.)
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// Whether the notification has been read
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// When the notification was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional expiration timestamp - notification can be auto-deleted after this time
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
