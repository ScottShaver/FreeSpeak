using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service providing business logic for managing user notifications.
    /// Handles notification creation, retrieval, read/unread status management, and cleanup operations.
    /// Includes DOS protection limiting bulk notification operations.
    /// </summary>
    public class NotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<NotificationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAuditLogRepository _auditLogRepository;

        /// <summary>
        /// Maximum number of recipients allowed per bulk notification operation to prevent resource exhaustion.
        /// </summary>
        private const int MaxBulkNotificationRecipients = 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService"/> class.
        /// </summary>
        /// <param name="notificationRepository">Repository for notification operations.</param>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="scopeFactory">Factory for creating service scopes.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        public NotificationService(
            INotificationRepository notificationRepository,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<NotificationService> logger,
            IServiceScopeFactory scopeFactory,
            IAuditLogRepository auditLogRepository)
        {
            _notificationRepository = notificationRepository;
            _contextFactory = contextFactory;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _auditLogRepository = auditLogRepository;
        }

        #region Create Notifications

        /// <summary>
        /// Creates a new notification for a user with automatic expiration based on user preferences.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to notify.</param>
        /// <param name="type">The type of notification.</param>
        /// <param name="message">The notification message content.</param>
        /// <param name="data">Optional additional data to serialize with the notification.</param>
        /// <param name="expiresAt">Optional expiration date; if not provided, calculated from user preferences.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created notification if successful.</returns>
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
        /// Creates notifications for multiple users in a single operation.
        /// Limited to 1000 recipients per call to prevent resource exhaustion.
        /// </summary>
        /// <param name="userIds">List of user IDs to receive the notification.</param>
        /// <param name="type">The type of notification.</param>
        /// <param name="message">The notification message content.</param>
        /// <param name="data">Optional additional data to serialize with each notification.</param>
        /// <param name="expiresAt">Optional expiration date for all notifications.</param>
        /// <returns>A tuple containing success status, error message if failed, and count of created notifications.</returns>
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

            // DOS PROTECTION: Limit number of recipients to prevent resource exhaustion
            if (userIds.Count > MaxBulkNotificationRecipients)
            {
                _logger.LogWarning("Bulk notification attempt with {Count} recipients exceeds limit of {Limit}", 
                    userIds.Count, MaxBulkNotificationRecipients);
                return (false, $"Maximum {MaxBulkNotificationRecipients} recipients allowed per bulk operation.", 0);
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
        /// Retrieves notifications for a user with pagination and optional read status filter.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="pageSize">Number of notifications per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <param name="isRead">Optional filter for read/unread status.</param>
        /// <returns>A list of notifications ordered by creation date descending.</returns>
        public async Task<List<UserNotification>> GetUserNotificationsAsync(
            string userId,
            int pageSize = 20,
            int pageNumber = 1,
            bool? isRead = null)
        {
            try
            {
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
        /// Gets the count of unread notifications for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The number of unread notifications.</returns>
        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                return await _notificationRepository.GetUnreadCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the total count of notifications for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The total number of notifications.</returns>
        public async Task<int> GetTotalCountAsync(string userId)
        {
            try
            {
                return await _notificationRepository.GetTotalCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Retrieves a notification by its unique identifier.
        /// </summary>
        /// <param name="notificationId">The unique identifier of the notification.</param>
        /// <returns>The notification if found; otherwise, null.</returns>
        public async Task<UserNotification?> GetNotificationByIdAsync(int notificationId)
        {
            try
            {
                return await _notificationRepository.GetByIdAsync(notificationId);
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
        /// Marks a specific notification as read.
        /// </summary>
        /// <param name="notificationId">The unique identifier of the notification.</param>
        /// <param name="userId">The user ID to verify ownership.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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

                    // Log notification mark as read to audit log
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.Read.ToString(),
                        NotificationType = notification.Type.ToString(),
                        NotificationId = notificationId,
                        DeliveryChannel = "InApp",
                        DeliverySuccess = true
                    });

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
        /// Marks all notifications as read for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A tuple containing success status, error message if failed, and count of updated notifications.</returns>
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

                    // Log bulk mark as read to audit log
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.BulkRead.ToString(), 
                        NotificationType = "Multiple",
                        ContentSummary = $"Marked {unreadNotifications.Count} notifications as read",
                        DeliveryChannel = "InApp",
                        DeliverySuccess = true
                    });

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
        /// Deletes a specific notification.
        /// </summary>
        /// <param name="notificationId">The unique identifier of the notification.</param>
        /// <param name="userId">The user ID to verify ownership.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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

                // Capture details before deletion
                var notificationType = notification.Type.ToString();

                context.UserNotifications.Remove(notification);
                await context.SaveChangesAsync();

                // Log notification deletion to audit log
                await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserNotification, new UserNotificationDetails
                {
                    OperationType = OperationTypeEnum.Delete.ToString(),
                    NotificationType = notificationType,
                    NotificationId = notificationId,
                    DeliveryChannel = "InApp",
                    DeliverySuccess = true
                });

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
        /// Deletes all read notifications for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A tuple containing success status, error message if failed, and count of deleted notifications.</returns>
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

                    // Log bulk notification deletion to audit log
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.BulkDeleted.ToString(),
                        NotificationType = "Multiple",
                        ContentSummary = $"Deleted {readNotifications.Count} read notifications",
                        DeliveryChannel = "InApp",
                        DeliverySuccess = true
                    });

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
        /// Deletes expired notifications across all users.
        /// Typically called by a background service or scheduled job.
        /// </summary>
        /// <returns>A tuple containing success status, error message if failed, and count of deleted notifications.</returns>
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
        /// Deserializes notification data to a specific type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the data to.</typeparam>
        /// <param name="notification">The notification containing the serialized data.</param>
        /// <returns>The deserialized data if successful; otherwise, null.</returns>
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
