using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for AuditLog entities.
    /// Provides operations for logging user actions and retrieving audit history.
    /// </summary>
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<AuditLogRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditLogRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public AuditLogRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<AuditLogRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new audit log entry for a user action.
        /// </summary>
        /// <param name="userId">The unique identifier of the user performing the action.</param>
        /// <param name="actionCategory">The category of the action (e.g., "Authentication", "ProfileUpdate").</param>
        /// <param name="actionDetails">JSON string containing detailed information about the action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LogActionAsync(string userId, string actionCategory, string actionDetails)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    ActionStamp = DateTime.UtcNow,
                    ActionCategory = actionCategory,
                    ActionDetails = actionDetails
                };

                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();

                _logger.LogDebug("Logged action {ActionCategory} for user {UserId}", actionCategory, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error logging action {ActionCategory} for user {UserId}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, actionCategory, userId, ex.Message);
                // Don't throw - audit logging should not break the main flow
            }
        }

        /// <summary>
        /// Retrieves audit log entries for a specific user, ordered by most recent first.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="pageNumber">The page number for pagination (1-based).</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <returns>A list of audit log entries for the user.</returns>
        public async Task<List<AuditLog>> GetUserAuditLogsAsync(string userId, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.AuditLogs
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.ActionStamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error retrieving audit logs for user {UserId}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, userId, ex.Message);
                return new List<AuditLog>();
            }
        }

        /// <summary>
        /// Retrieves the total count of audit log entries for a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The total number of audit log entries for the user.</returns>
        public async Task<int> GetUserAuditLogCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.AuditLogs
                    .Where(a => a.UserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error counting audit logs for user {UserId}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, userId, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Retrieves audit log entries for a specific user within a date range.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="startDate">The start date of the range (inclusive).</param>
        /// <param name="endDate">The end date of the range (inclusive).</param>
        /// <returns>A list of audit log entries within the specified date range.</returns>
        public async Task<List<AuditLog>> GetUserAuditLogsByDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.AuditLogs
                    .Where(a => a.UserId == userId 
                             && a.ActionStamp >= startDate 
                             && a.ActionStamp <= endDate)
                    .OrderByDescending(a => a.ActionStamp)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error retrieving audit logs for user {UserId} between {StartDate} and {EndDate}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, userId, startDate, endDate, ex.Message);
                return new List<AuditLog>();
            }
        }

        /// <summary>
        /// Retrieves audit log entries for a specific action category across all users.
        /// </summary>
        /// <param name="actionCategory">The action category to filter by.</param>
        /// <param name="pageNumber">The page number for pagination (1-based).</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <returns>A list of audit log entries matching the category.</returns>
        public async Task<List<AuditLog>> GetAuditLogsByCategoryAsync(string actionCategory, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.AuditLogs
                    .Where(a => a.ActionCategory == actionCategory)
                    .OrderByDescending(a => a.ActionStamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error retrieving audit logs for category {ActionCategory}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, actionCategory, ex.Message);
                return new List<AuditLog>();
            }
        }

        /// <summary>
        /// Searches audit log entries with optional filters for action category and date range.
        /// Returns the most recent entries up to a maximum of 500 records.
        /// </summary>
        /// <param name="actionCategory">Optional action category filter (e.g., "Authentication", "ProfileUpdate"). If null or empty, all categories are included.</param>
        /// <param name="startDate">Optional start date for the search range (inclusive). If null, no start date filter is applied.</param>
        /// <param name="endDate">Optional end date for the search range (inclusive). If null, no end date filter is applied.</param>
        /// <returns>A list of up to 500 audit log entries matching the search criteria, ordered by most recent first.</returns>
        public async Task<List<AuditLog>> SearchAuditLogsAsync(string? actionCategory = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Start with base query
                var query = context.AuditLogs.AsQueryable();

                // Apply action category filter if provided
                if (!string.IsNullOrWhiteSpace(actionCategory))
                {
                    query = query.Where(a => a.ActionCategory == actionCategory);
                }

                // Apply start date filter if provided
                if (startDate.HasValue)
                {
                    query = query.Where(a => a.ActionStamp >= startDate.Value);
                }

                // Apply end date filter if provided
                if (endDate.HasValue)
                {
                    // Include the entire end date by adding one day and using less than comparison
                    var endDateInclusive = endDate.Value.Date.AddDays(1);
                    query = query.Where(a => a.ActionStamp < endDateInclusive);
                }

                // Order by most recent first and limit to 500 records
                var results = await query
                    .OrderByDescending(a => a.ActionStamp)
                    .Take(500)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogDebug("Search returned {Count} audit log entries with filters: Category={Category}, StartDate={StartDate}, EndDate={EndDate}",
                    results.Count, actionCategory ?? "All", startDate?.ToString("yyyy-MM-dd") ?? "None", endDate?.ToString("yyyy-MM-dd") ?? "None");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{ExceptionType}] Error searching audit logs with filters: Category={Category}, StartDate={StartDate}, EndDate={EndDate}. Exception: {ExceptionMessage}",
                    ex.GetType().Name, actionCategory ?? "All", startDate?.ToString("yyyy-MM-dd") ?? "None", endDate?.ToString("yyyy-MM-dd") ?? "None", ex.Message);
                return new List<AuditLog>();
            }
        }
    }
}
