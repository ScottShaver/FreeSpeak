using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for Group entity
    /// </summary>
    public class GroupRepository : IGroupRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupRepository> _logger;

        public GroupRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<Group?> GetByIdAsync(int id)
        {
            return await GetByIdAsync(id, false, false);
        }

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
