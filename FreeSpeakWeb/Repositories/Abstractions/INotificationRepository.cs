using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for UserNotification entity
    /// </summary>
    public interface INotificationRepository : IRepository<UserNotification>
    {
        /// <summary>
        /// Get notifications for a user
        /// </summary>
        Task<List<UserNotification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 50, bool unreadOnly = false);

        /// <summary>
        /// Get unread notification count for a user
        /// </summary>
        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>
        /// Get total notification count for a user
        /// </summary>
        Task<int> GetTotalCountAsync(string userId);

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        Task<bool> MarkAsReadAsync(int notificationId, string userId);

        /// <summary>
        /// Mark all notifications as read for a user
        /// </summary>
        Task<int> MarkAllAsReadAsync(string userId);

        /// <summary>
        /// Delete a notification
        /// </summary>
        Task<bool> DeleteNotificationAsync(int notificationId, string userId);

        /// <summary>
        /// Delete all read notifications for a user
        /// </summary>
        Task<int> DeleteReadNotificationsAsync(string userId);

        /// <summary>
        /// Delete expired notifications
        /// </summary>
        Task<int> DeleteExpiredNotificationsAsync();

        /// <summary>
        /// Bulk create notifications for multiple users
        /// </summary>
        Task<int> BulkCreateNotificationsAsync(IEnumerable<UserNotification> notifications);

        /// <summary>
        /// Get notification by type for a specific entity
        /// </summary>
        Task<UserNotification?> GetNotificationByTypeAsync(string userId, NotificationType type, string? relatedEntityId = null);
    }
}
