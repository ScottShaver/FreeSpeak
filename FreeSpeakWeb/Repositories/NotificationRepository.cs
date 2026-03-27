using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
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
        private readonly ProfilerHelper _profiler;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        /// <param name="profiler">Helper for profiling repository operations.</param>
        public NotificationRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<NotificationRepository> logger,
            ProfilerHelper profiler)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _profiler = profiler;
        }

        public async Task<UserNotification?> GetByIdAsync(int id)
        {
            using var step = _profiler.Step($"NotificationRepository.GetByIdAsync({id})");
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
            using var step = _profiler.Step("NotificationRepository.GetAllAsync");
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
            using var step = _profiler.Step("NotificationRepository.AddAsync");
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
            using var step = _profiler.Step($"NotificationRepository.UpdateAsync({entity.Id})");
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
            using var step = _profiler.Step($"NotificationRepository.DeleteAsync({entity.Id})");
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
            using var step = _profiler.Step($"NotificationRepository.ExistsAsync({id})");
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
            using var step = _profiler.Step($"NotificationRepository.GetUserNotificationsAsync({userId})");
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
            using var step = _profiler.Step($"NotificationRepository.GetUnreadCountAsync({userId})");
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
            using var step = _profiler.Step($"NotificationRepository.GetTotalCountAsync({userId})");
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
            using var step = _profiler.Step($"NotificationRepository.MarkAsReadAsync({notificationId})");
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
            using var step = _profiler.Step($"NotificationRepository.MarkAllAsReadAsync({userId})");
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
            using var step = _profiler.Step($"NotificationRepository.DeleteNotificationAsync({notificationId})");
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
            using var step = _profiler.Step($"NotificationRepository.DeleteReadNotificationsAsync({userId})");
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
            using var step = _profiler.Step("NotificationRepository.DeleteExpiredNotificationsAsync");
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
            using var step = _profiler.Step("NotificationRepository.BulkCreateNotificationsAsync");
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
            using var step = _profiler.Step($"NotificationRepository.GetNotificationByTypeAsync({userId})");
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
