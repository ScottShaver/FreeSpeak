using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing UserNotification entities.
    /// Provides methods for creating, retrieving, marking as read, and deleting notifications.
    /// Supports bulk operations and automatic cleanup of expired notifications.
    /// </summary>
    public interface INotificationRepository : IRepository<UserNotification>
    {
        /// <summary>
        /// Retrieves notifications for a specific user with pagination and filtering options.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="skip">Number of notifications to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of notifications to retrieve. Default is 50.</param>
        /// <param name="unreadOnly">If true, returns only unread notifications. Default is false.</param>
        /// <returns>A list of notifications for the specified user, ordered by creation date descending.</returns>
        Task<List<UserNotification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 50, bool unreadOnly = false);

        /// <summary>
        /// Gets the count of unread notifications for a user.
        /// Used for badge display in the UI.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The number of unread notifications.</returns>
        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>
        /// Gets the total count of all notifications for a user (read and unread).
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The total number of notifications.</returns>
        Task<int> GetTotalCountAsync(string userId);

        /// <summary>
        /// Marks a specific notification as read.
        /// Validates that the notification belongs to the specified user.
        /// </summary>
        /// <param name="notificationId">The ID of the notification.</param>
        /// <param name="userId">The ID of the user marking the notification as read.</param>
        /// <returns>True if the notification was successfully marked as read; otherwise, false.</returns>
        Task<bool> MarkAsReadAsync(int notificationId, string userId);

        /// <summary>
        /// Marks all notifications for a user as read.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The number of notifications that were marked as read.</returns>
        Task<int> MarkAllAsReadAsync(string userId);

        /// <summary>
        /// Deletes a specific notification.
        /// Validates that the notification belongs to the specified user.
        /// </summary>
        /// <param name="notificationId">The ID of the notification to delete.</param>
        /// <param name="userId">The ID of the user deleting the notification.</param>
        /// <returns>True if the notification was successfully deleted; otherwise, false.</returns>
        Task<bool> DeleteNotificationAsync(int notificationId, string userId);

        /// <summary>
        /// Deletes all read notifications for a user.
        /// Helps keep the notification list clean and manageable.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The number of notifications that were deleted.</returns>
        Task<int> DeleteReadNotificationsAsync(string userId);

        /// <summary>
        /// Deletes all notifications that have passed their expiration date.
        /// Should be called periodically by a background service for automatic cleanup.
        /// </summary>
        /// <returns>The number of expired notifications that were deleted.</returns>
        Task<int> DeleteExpiredNotificationsAsync();

        /// <summary>
        /// Creates multiple notifications in a single database operation.
        /// Efficient for sending the same notification to multiple users.
        /// </summary>
        /// <param name="notifications">Collection of notification entities to create.</param>
        /// <returns>The number of notifications that were successfully created.</returns>
        Task<int> BulkCreateNotificationsAsync(IEnumerable<UserNotification> notifications);

        /// <summary>
        /// Retrieves a specific notification by type and optional related entity ID.
        /// Useful for preventing duplicate notifications.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="type">The type of notification to search for.</param>
        /// <param name="relatedEntityId">Optional ID of the related entity (e.g., post ID, comment ID).</param>
        /// <returns>The notification if found; otherwise, null.</returns>
        Task<UserNotification?> GetNotificationByTypeAsync(string userId, NotificationType type, string? relatedEntityId = null);
    }
}
