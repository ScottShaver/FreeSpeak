namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a notification sent to a user about system events, social interactions, or updates.
    /// Supports various notification types and can carry additional metadata in JSON format.
    /// </summary>
    public class UserNotification
    {
        /// <summary>
        /// Gets or sets the unique identifier for the notification.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user this notification is intended for.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the recipient user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the type of notification (e.g., Comment, Like, FriendRequest).
        /// Used to categorize and route notifications appropriately.
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// Gets or sets the template key used to render the notification message in different languages.
        /// When set, the message is rendered at display time using the recipient's language preference.
        /// If null, the Message field is used directly (for backward compatibility).
        /// </summary>
        public string? TemplateKey { get; set; }

        /// <summary>
        /// Gets or sets the notification message text displayed to the user.
        /// For new notifications, this may be null if TemplateKey is used instead.
        /// For legacy notifications, this contains the pre-formatted message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets additional JSON-serialized data for the notification.
        /// Can include post IDs, user IDs, URLs, or other contextual information.
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// Gets or sets whether the user has read this notification.
        /// Defaults to false for new notifications.
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Gets or sets the timestamp when the notification was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets an optional expiration timestamp.
        /// Notifications can be automatically deleted after this time to reduce clutter.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
