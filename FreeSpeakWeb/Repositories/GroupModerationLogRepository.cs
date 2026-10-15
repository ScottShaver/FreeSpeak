using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IGroupModerationLogRepository"/>.
    /// </summary>
    public class GroupModerationLogRepository : IGroupModerationLogRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupModerationLogRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        public GroupModerationLogRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public async Task AddAsync(GroupModerationLogEntry entry, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.GroupModerationLogEntries.Add(entry);
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<(List<GroupModerationLogEntry> Items, int TotalCount)> GetPagedAsync(
            int groupId,
            GroupModerationLogFilter filter,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = ApplyFilter(context.GroupModerationLogEntries.AsNoTracking(), groupId, filter);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .Skip((Math.Max(pageNumber, 1) - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        /// <inheritdoc/>
        public async Task<List<GroupModerationLogEntry>> GetForExportAsync(
            int groupId,
            GroupModerationLogFilter filter,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await ApplyFilter(context.GroupModerationLogEntries.AsNoTracking(), groupId, filter)
                .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .Take(maxRows)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds,
            CancellationToken cancellationToken = default)
        {
            var ids = userIds.Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var users = await context.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
                .ToListAsync(cancellationToken);

            return users.ToDictionary(
                u => u.Id,
                u =>
                {
                    var full = $"{u.FirstName} {u.LastName}".Trim();
                    return string.IsNullOrWhiteSpace(full) ? (u.UserName ?? u.Id) : full;
                });
        }

        /// <inheritdoc/>
        public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            var total = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var batch = await context.GroupModerationLogEntries
                    .Where(e => e.CreatedAt < cutoffUtc)
                    .OrderBy(e => e.Id)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                context.GroupModerationLogEntries.RemoveRange(batch);
                await context.SaveChangesAsync(cancellationToken);
                total += batch.Count;

                if (batch.Count < batchSize)
                {
                    break;
                }
            }

            return total;
        }

        /// <summary>
        /// Applies the group and optional filters to a query.
        /// </summary>
        /// <param name="query">The base query.</param>
        /// <param name="groupId">The group id.</param>
        /// <param name="filter">The filters.</param>
        /// <returns>The filtered query.</returns>
        private static IQueryable<GroupModerationLogEntry> ApplyFilter(
            IQueryable<GroupModerationLogEntry> query, int groupId, GroupModerationLogFilter filter)
        {
            query = query.Where(e => e.GroupId == groupId);

            if (filter.Action.HasValue)
            {
                var action = filter.Action.Value;
                query = query.Where(e => e.Action == action);
            }

            if (!string.IsNullOrEmpty(filter.ActorUserId))
            {
                var actor = filter.ActorUserId;
                query = query.Where(e => e.ActorUserId == actor);
            }

            if (filter.FromDate.HasValue)
            {
                var from = filter.FromDate.Value;
                query = query.Where(e => e.CreatedAt >= from);
            }

            if (filter.ToDate.HasValue)
            {
                var to = filter.ToDate.Value;
                query = query.Where(e => e.CreatedAt <= to);
            }

            return query;
        }
    }
}
