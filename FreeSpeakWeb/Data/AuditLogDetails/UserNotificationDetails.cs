namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user notification audit log entries.
    /// Tracks notification creation, delivery, and user interaction with notifications.
    /// </summary>
    public class UserNotificationDetails
    {
        /// <summary>
        /// Gets or sets the type of notification action.
        /// Examples: "Created", "Delivered", "Read", "Dismissed", "Clicked".
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of notification.
        /// Examples: "FriendRequest", "GroupInvite", "PostLike", "Comment", "System".
        /// </summary>
        public string NotificationType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the notification, if applicable.
        /// </summary>
        public int? NotificationId { get; set; }

        /// <summary>
        /// Gets or sets a brief summary of the notification content.
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// Gets or sets the delivery channel used for the notification.
        /// Examples: "InApp", "Email", "Push", "SMS".
        /// </summary>
        public string? DeliveryChannel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the notification was successfully delivered.
        /// </summary>
        public bool DeliverySuccess { get; set; }
    }
}
