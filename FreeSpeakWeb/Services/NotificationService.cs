using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FreeSpeakWeb.Services
{
    public class NotificationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<NotificationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<NotificationService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        #region Create Notifications

        /// <summary>
        /// Create a new notification for a user with automatic expiration based on user preferences
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, UserNotification? Notification)> CreateNotificationAsync(
            string userId,
            NotificationType type,
            string message,
            object? data = null,
            DateTime? expiresAt = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, "User ID is required.", null);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return (false, "Message is required.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify user exists
                var userExists = await context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    return (false, "User not found.", null);
                }

                // If no expiration provided, calculate from user preferences
                if (expiresAt == null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var preferenceService = scope.ServiceProvider.GetRequiredService<UserPreferenceService>();
                    var expirationDays = await preferenceService.GetNotificationExpirationDaysAsync(userId, type);
                    expiresAt = DateTime.UtcNow.AddDays(expirationDays);
                }

                var notification = new UserNotification
                {
                    UserId = userId,
                    Type = type,
                    Message = message,
                    Data = data != null ? JsonSerializer.Serialize(data) : null,
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                context.UserNotifications.Add(notification);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notification created for user {UserId}: Type {Type}, Expires {ExpiresAt}", 
                    userId, type, expiresAt);
                return (true, null, notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {UserId}", userId);
                return (false, "An error occurred while creating the notification.", null);
            }
        }

        /// <summary>
        /// Create notifications for multiple users
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int CreatedCount)> CreateBulkNotificationsAsync(
            List<string> userIds,
            NotificationType type,
            string message,
            object? data = null,
            DateTime? expiresAt = null)
        {
            if (userIds == null || !userIds.Any())
            {
                return (false, "At least one user ID is required.", 0);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return (false, "Message is required.", 0);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify users exist
                var existingUserIds = await context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (!existingUserIds.Any())
                {
                    return (false, "No valid users found.", 0);
                }

                var serializedData = data != null ? JsonSerializer.Serialize(data) : null;
                var notifications = existingUserIds.Select(userId => new UserNotification
                {
                    UserId = userId,
                    Type = type,
                    Message = message,
                    Data = serializedData,
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                }).ToList();

                context.UserNotifications.AddRange(notifications);
                await context.SaveChangesAsync();

                _logger.LogInformation("Bulk notifications created: {Count} notifications of type {Type}", notifications.Count, type);
                return (true, null, notifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bulk notifications");
                return (false, "An error occurred while creating notifications.", 0);
            }
        }

        #endregion

        #region Retrieve Notifications

        /// <summary>
        /// Get notifications for a user with pagination
        /// </summary>
        public async Task<List<UserNotification>> GetUserNotificationsAsync(
            string userId,
            int pageSize = 20,
            int pageNumber = 1,
            bool? isRead = null)
        {
            try
            {
                // Clean up expired notifications before retrieving the list
                await DeleteExpiredNotificationsAsync();

                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.UserNotifications
                    .Where(n => n.UserId == userId);

                if (isRead.HasValue)
                {
                    query = query.Where(n => n.IsRead == isRead.Value);
                }

                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
                return new List<UserNotification>();
            }
        }

        /// <summary>
        /// Get unread notification count for a user
        /// </summary>
        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                // Clean up expired notifications before counting
                await DeleteExpiredNotificationsAsync();

                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.UserNotifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Get total notification count for a user
        /// </summary>
        public async Task<int> GetTotalCountAsync(string userId)
        {
            try
            {
                // Clean up expired notifications before counting
                await DeleteExpiredNotificationsAsync();

                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.UserNotifications
                    .Where(n => n.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Get a notification by ID
        /// </summary>
        public async Task<UserNotification?> GetNotificationByIdAsync(int notificationId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification {NotificationId}", notificationId);
                return null;
            }
        }

        #endregion

        #region Update Notifications

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> MarkAsReadAsync(int notificationId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var notification = await context.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    return (false, "Notification not found.");
                }

                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Notification {NotificationId} marked as read by user {UserId}", notificationId, userId);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
                return (false, "An error occurred while updating the notification.");
            }
        }

        /// <summary>
        /// Mark all notifications as read for a user
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int UpdatedCount)> MarkAllAsReadAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var unreadNotifications = await context.UserNotifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                if (unreadNotifications.Any())
                {
                    foreach (var notification in unreadNotifications)
                    {
                        notification.IsRead = true;
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", unreadNotifications.Count, userId);
                }

                return (true, null, unreadNotifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                return (false, "An error occurred while updating notifications.", 0);
            }
        }

        #endregion

        #region Delete Notifications

        /// <summary>
        /// Delete a specific notification
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> DeleteNotificationAsync(int notificationId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var notification = await context.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    return (false, "Notification not found.");
                }

                context.UserNotifications.Remove(notification);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notification {NotificationId} deleted by user {UserId}", notificationId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}", notificationId, userId);
                return (false, "An error occurred while deleting the notification.");
            }
        }

        /// <summary>
        /// Delete all read notifications for a user
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteReadNotificationsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var readNotifications = await context.UserNotifications
                    .Where(n => n.UserId == userId && n.IsRead)
                    .ToListAsync();

                if (readNotifications.Any())
                {
                    context.UserNotifications.RemoveRange(readNotifications);
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Deleted {Count} read notifications for user {UserId}", readNotifications.Count, userId);
                }

                return (true, null, readNotifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting read notifications for user {UserId}", userId);
                return (false, "An error occurred while deleting notifications.", 0);
            }
        }

        /// <summary>
        /// Delete expired notifications across all users
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteExpiredNotificationsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var now = DateTime.UtcNow;
                var expiredNotifications = await context.UserNotifications
                    .Where(n => n.ExpiresAt.HasValue && n.ExpiresAt.Value < now)
                    .ToListAsync();

                if (expiredNotifications.Any())
                {
                    context.UserNotifications.RemoveRange(expiredNotifications);
                    await context.SaveChangesAsync();
                    _logger.LogInformation("Deleted {Count} expired notifications", expiredNotifications.Count);
                }

                return (true, null, expiredNotifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expired notifications");
                return (false, "An error occurred while deleting expired notifications.", 0);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Deserialize notification data to a specific type
        /// </summary>
        public T? GetNotificationData<T>(UserNotification notification) where T : class
        {
            if (string.IsNullOrWhiteSpace(notification.Data))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(notification.Data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize notification data for notification {NotificationId}", notification.Id);
                return null;
            }
        }

        #endregion
    }
}
