using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for Group entities.
    /// Provides operations for creating, retrieving, updating, and deleting groups,
    /// as well as membership and search functionality.
    /// </summary>
    public class GroupRepository : IGroupRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public GroupRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a group by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the group.</param>
        /// <returns>The group entity if found; otherwise, null.</returns>
        public async Task<Group?> GetByIdAsync(int id)
        {
            return await GetByIdAsync(id, false, false);
        }

        /// <summary>
        /// Retrieves a group by its unique identifier with optional navigation property loading.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="includeMembers">Whether to include member information (not currently implemented).</param>
        /// <param name="includeRules">Whether to include group rules (not currently implemented).</param>
        /// <returns>The group entity with creator details if found; otherwise, null.</returns>
        public async Task<Group?> GetByIdAsync(int groupId, bool includeMembers = false, bool includeRules = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups
                    .Include(g => g.Creator)
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                // Note: Members and Rules navigation properties would need to be added to Group entity
                // For now, these can be loaded separately if needed using GroupUsers and GroupRules tables

                return group;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group {GroupId}", groupId);
                return null;
            }
        }

        /// <summary>
        /// Retrieves all groups in the system.
        /// </summary>
        /// <returns>A list of all groups with creator details.</returns>
        public async Task<List<Group>> GetAllAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Groups
                    .Include(g => g.Creator)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all groups");
                return new List<Group>();
            }
        }

        /// <summary>
        /// Adds a new group entity to the database.
        /// </summary>
        /// <param name="entity">The group entity to add.</param>
        /// <returns>The added group entity.</returns>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task<Group> AddAsync(Group entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Groups.Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding group");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing group entity in the database.
        /// </summary>
        /// <param name="entity">The group entity with updated values.</param>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task UpdateAsync(Group entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Groups.Update(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId}", entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a group entity from the database.
        /// </summary>
        /// <param name="entity">The group entity to delete.</param>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task DeleteAsync(Group entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Groups.Remove(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {GroupId}", entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Checks whether a group with the specified ID exists.
        /// </summary>
        /// <param name="id">The unique identifier of the group.</param>
        /// <returns>True if the group exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Groups.AnyAsync(g => g.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of group {GroupId}", id);
                return false;
            }
        }

        /// <summary>
        /// Retrieves public, non-hidden groups with pagination support.
        /// </summary>
        /// <param name="skip">Number of groups to skip for pagination.</param>
        /// <param name="take">Number of groups to return.</param>
        /// <returns>A list of public groups ordered by last activity descending.</returns>
        public async Task<List<Group>> GetPublicGroupsAsync(int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.IsPublic && !g.IsHidden)
                    .OrderByDescending(g => g.LastActiveAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public groups");
                return new List<Group>();
            }
        }

        /// <summary>
        /// Retrieves groups that a user is a member of with pagination support.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="skip">Number of groups to skip for pagination.</param>
        /// <param name="take">Number of groups to return.</param>
        /// <returns>A list of groups the user belongs to, ordered by last activity descending.</returns>
        public async Task<List<Group>> GetUserGroupsAsync(string userId, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get group IDs where user is a member
                var groupIds = await context.GroupUsers
                    .Where(gu => gu.UserId == userId)
                    .Select(gu => gu.GroupId)
                    .ToListAsync();

                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => groupIds.Contains(g.Id))
                    .OrderByDescending(g => g.LastActiveAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups for user {UserId}", userId);
                return new List<Group>();
            }
        }

        /// <summary>
        /// Retrieves groups created by a specific user with pagination support.
        /// </summary>
        /// <param name="userId">The unique identifier of the creator.</param>
        /// <param name="skip">Number of groups to skip for pagination.</param>
        /// <param name="take">Number of groups to return.</param>
        /// <returns>A list of groups created by the user, ordered by creation date descending.</returns>
        public async Task<List<Group>> GetGroupsCreatedByUserAsync(string userId, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.CreatorId == userId)
                    .OrderByDescending(g => g.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups created by user {UserId}", userId);
                return new List<Group>();
            }
        }

        /// <summary>
        /// Searches for public, non-hidden groups by name or description.
        /// </summary>
        /// <param name="searchTerm">The term to search for in group names and descriptions.</param>
        /// <param name="skip">Number of groups to skip for pagination.</param>
        /// <param name="take">Number of groups to return.</param>
        /// <returns>A list of matching groups ordered by member count descending.</returns>
        public async Task<List<Group>> SearchGroupsAsync(string searchTerm, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var searchLower = searchTerm.ToLower();

                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.IsPublic && !g.IsHidden &&
                        (g.Name.ToLower().Contains(searchLower) || g.Description.ToLower().Contains(searchLower)))
                    .OrderByDescending(g => g.MemberCount)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching groups with term {SearchTerm}", searchTerm);
                return new List<Group>();
            }
        }

        /// <summary>
        /// Checks whether a user is a member of a specific group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is a member; otherwise, false.</returns>
        public async Task<bool> IsUserMemberAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupUsers.AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is member of group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Checks whether a user is the creator of a specific group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is the creator; otherwise, false.</returns>
        public async Task<bool> IsUserCreatorAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var group = await context.Groups.FindAsync(groupId);
                return group?.CreatorId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is creator of group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Checks whether a user is an administrator of a specific group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is an admin; otherwise, false.</returns>
        public async Task<bool> IsUserAdminAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var membership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                return membership?.IsAdmin == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is admin of group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Gets the total count of members in a group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The number of members in the group.</returns>
        public async Task<int> GetMemberCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupUsers.CountAsync(gu => gu.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting members for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <summary>
        /// Updates the last active timestamp for a group to the current time.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        public async Task UpdateLastActiveAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var group = await context.Groups.FindAsync(groupId);
                if (group != null)
                {
                    group.LastActiveAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active timestamp for group {GroupId}", groupId);
            }
        }
    }
}
