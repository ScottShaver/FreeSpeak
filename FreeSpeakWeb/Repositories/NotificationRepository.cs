using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for UserNotification entity
    /// </summary>
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<NotificationRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<UserNotification?> GetByIdAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserNotifications.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification {NotificationId}", id);
                return null;
            }
        }

        public async Task<List<UserNotification>> GetAllAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserNotifications.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all notifications");
                return new List<UserNotification>();
            }
        }

        public async Task<UserNotification> AddAsync(UserNotification entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.UserNotifications.Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notification");
                throw;
            }
        }

        public async Task UpdateAsync(UserNotification entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.UserNotifications.Update(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification {NotificationId}", entity.Id);
                throw;
            }
        }

        public async Task DeleteAsync(UserNotification entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.UserNotifications.Remove(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId}", entity.Id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserNotifications.AnyAsync(n => n.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of notification {NotificationId}", id);
                return false;
            }
        }

        public async Task<List<UserNotification>> GetUserNotificationsAsync(string userId, int skip = 0, int take = 50, bool unreadOnly = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.UserNotifications.Where(n => n.UserId == userId);

                if (unreadOnly)
                    query = query.Where(n => !n.IsRead);

                return await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
                return new List<UserNotification>();
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting unread notifications for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> GetTotalCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.UserNotifications.CountAsync(n => n.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting total notifications for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var notification = await context.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                notification.IsRead = true;
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
                return false;
            }
        }

        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var notifications = await context.UserNotifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                await context.SaveChangesAsync();
                return notifications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var notification = await context.UserNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                    return false;

                context.UserNotifications.Remove(notification);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
                return false;
            }
        }

        public async Task<int> DeleteReadNotificationsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var notifications = await context.UserNotifications
                    .Where(n => n.UserId == userId && n.IsRead)
                    .ToListAsync();

                context.UserNotifications.RemoveRange(notifications);
                await context.SaveChangesAsync();
                return notifications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting read notifications for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<int> DeleteExpiredNotificationsAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var now = DateTime.UtcNow;
                var expiredNotifications = await context.UserNotifications
                    .Where(n => n.ExpiresAt.HasValue && n.ExpiresAt.Value < now)
                    .ToListAsync();

                context.UserNotifications.RemoveRange(expiredNotifications);
                await context.SaveChangesAsync();

                _logger.LogInformation("Deleted {Count} expired notifications", expiredNotifications.Count);
                return expiredNotifications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expired notifications");
                return 0;
            }
        }

        public async Task<int> BulkCreateNotificationsAsync(IEnumerable<UserNotification> notifications)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var notificationList = notifications.ToList();

                context.UserNotifications.AddRange(notificationList);
                await context.SaveChangesAsync();

                return notificationList.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating notifications");
                return 0;
            }
        }

        public async Task<UserNotification?> GetNotificationByTypeAsync(string userId, NotificationType type, string? relatedEntityId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.UserNotifications
                    .Where(n => n.UserId == userId && n.Type == type);

                if (relatedEntityId != null)
                {
                    query = query.Where(n => n.Data != null && n.Data.Contains(relatedEntityId));
                }

                return await query.OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification by type {Type} for user {UserId}", type, userId);
                return null;
            }
        }
    }
}
