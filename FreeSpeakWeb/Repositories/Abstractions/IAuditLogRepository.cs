using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing AuditLog entities.
    /// Provides methods for logging user actions, retrieving audit logs, and querying historical activity.
    /// </summary>
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Creates a new audit log entry for a user action.
        /// </summary>
        /// <param name="userId">The unique identifier of the user performing the action.</param>
        /// <param name="actionCategory">The category of the action (e.g., "Authentication", "ProfileUpdate").</param>
        /// <param name="actionDetails">JSON string containing detailed information about the action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogActionAsync(string userId, string actionCategory, string actionDetails);

        /// <summary>
        /// Creates a new audit log entry for a user action using strongly-typed category and details.
        /// </summary>
        /// <typeparam name="TDetails">The type of the action details object (from AuditLogDetails namespace).</typeparam>
        /// <param name="userId">The unique identifier of the user performing the action.</param>
        /// <param name="actionCategory">The category of the action from the ActionCategory enum.</param>
        /// <param name="details">The strongly-typed details object containing action-specific information.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogActionAsync<TDetails>(string userId, ActionCategory actionCategory, TDetails details) where TDetails : class;

        /// <summary>
        /// Retrieves audit log entries for a specific user, ordered by most recent first.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="pageNumber">The page number for pagination (1-based).</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <returns>A list of audit log entries for the user.</returns>
        Task<List<AuditLog>> GetUserAuditLogsAsync(string userId, int pageNumber = 1, int pageSize = 50);

        /// <summary>
        /// Retrieves the total count of audit log entries for a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The total number of audit log entries for the user.</returns>
        Task<int> GetUserAuditLogCountAsync(string userId);

        /// <summary>
        /// Retrieves audit log entries for a specific user within a date range.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="startDate">The start date of the range (inclusive).</param>
        /// <param name="endDate">The end date of the range (inclusive).</param>
        /// <returns>A list of audit log entries within the specified date range.</returns>
        Task<List<AuditLog>> GetUserAuditLogsByDateRangeAsync(string userId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Retrieves audit log entries for a specific action category across all users.
        /// </summary>
        /// <param name="actionCategory">The action category to filter by.</param>
        /// <param name="pageNumber">The page number for pagination (1-based).</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <returns>A list of audit log entries matching the category.</returns>
        Task<List<AuditLog>> GetAuditLogsByCategoryAsync(string actionCategory, int pageNumber = 1, int pageSize = 50);

        /// <summary>
        /// Searches audit log entries with optional filters for action category and date range.
        /// Returns the most recent entries up to a maximum of 500 records.
        /// </summary>
        /// <param name="actionCategory">Optional action category filter (e.g., "Authentication", "ProfileUpdate"). If null or empty, all categories are included.</param>
        /// <param name="startDate">Optional start date for the search range (inclusive). If null, no start date filter is applied.</param>
        /// <param name="endDate">Optional end date for the search range (inclusive). If null, no end date filter is applied.</param>
        /// <returns>A list of up to 500 audit log entries matching the search criteria, ordered by most recent first.</returns>
        Task<List<AuditLog>> SearchAuditLogsAsync(string? actionCategory = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
