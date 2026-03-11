using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class GroupService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupService> _logger;

        public GroupService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        #region Create Groups

        /// <summary>
        /// Create a new group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, Group? Group)> CreateGroupAsync(
            string creatorId,
            string name,
            string description,
            bool isPublic = true,
            bool isHidden = false,
            bool requiresJoinApproval = false,
            string? headerImageUrl = null,
            string? verticalHeaderImageUrl = null,
            string? websiteUrl = null)
        {
            if (string.IsNullOrWhiteSpace(creatorId))
            {
                return (false, "Creator ID is required.", null);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Group name is required.", null);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return (false, "Group description is required.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify creator exists
                var creatorExists = await context.Users.AnyAsync(u => u.Id == creatorId);
                if (!creatorExists)
                {
                    return (false, "Creator user not found.", null);
                }

                // Check for duplicate group name
                var nameExists = await context.Groups.AnyAsync(g => g.Name == name);
                if (nameExists)
                {
                    return (false, "A group with this name already exists.", null);
                }

                var group = new Group
                {
                    CreatorId = creatorId,
                    Name = name,
                    Description = description,
                    IsPublic = isPublic,
                    IsHidden = isHidden,
                    RequiresJoinApproval = requiresJoinApproval,
                    HeaderImageUrl = headerImageUrl,
                    VerticalHeaderImageUrl = verticalHeaderImageUrl,
                    WebsiteUrl = websiteUrl,
                    CreatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    MemberCount = 0
                };

                context.Groups.Add(group);
                await context.SaveChangesAsync();

                _logger.LogInformation("Group created: {GroupId} '{GroupName}' by user {CreatorId}", 
                    group.Id, group.Name, creatorId);

                return (true, null, group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group for user {CreatorId}", creatorId);
                return (false, "An error occurred while creating the group.", null);
            }
        }

        #endregion

        #region Retrieve Groups

        /// <summary>
        /// Get a group by ID
        /// </summary>
        public async Task<Group?> GetGroupByIdAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Groups
                    .Include(g => g.Creator)
                    .FirstOrDefaultAsync(g => g.Id == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group {GroupId}", groupId);
                return null;
            }
        }

        /// <summary>
        /// Get all public, non-hidden groups with pagination
        /// </summary>
        public async Task<List<Group>> GetPublicGroupsAsync(
            int pageSize = 20,
            int pageNumber = 1,
            string? searchTerm = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.IsPublic && !g.IsHidden);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(g => 
                        g.Name.Contains(searchTerm) || 
                        g.Description.Contains(searchTerm));
                }

                return await query
                    .OrderByDescending(g => g.LastActiveAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public groups");
                return new List<Group>();
            }
        }

        /// <summary>
        /// Get groups created by a specific user
        /// </summary>
        public async Task<List<Group>> GetGroupsByCreatorAsync(string creatorId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Groups
                    .Where(g => g.CreatorId == creatorId)
                    .OrderByDescending(g => g.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups for creator {CreatorId}", creatorId);
                return new List<Group>();
            }
        }

        /// <summary>
        /// Get total count of public groups
        /// </summary>
        public async Task<int> GetPublicGroupCountAsync(string? searchTerm = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Groups
                    .Where(g => g.IsPublic && !g.IsHidden);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(g => 
                        g.Name.Contains(searchTerm) || 
                        g.Description.Contains(searchTerm));
                }

                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public group count");
                return 0;
            }
        }

        #endregion

        #region Update Groups

        /// <summary>
        /// Update group information
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UpdateGroupAsync(
            int groupId,
            string userId,
            string? name = null,
            string? description = null,
            bool? isPublic = null,
            bool? isHidden = null,
            bool? requiresJoinApproval = null,
            string? headerImageUrl = null,
            string? verticalHeaderImageUrl = null,
            string? websiteUrl = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    return (false, "Group not found.");
                }

                // Verify user is the creator
                if (group.CreatorId != userId)
                {
                    return (false, "Only the group creator can update group settings.");
                }

                // Update fields if provided
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // Check for duplicate name
                    var nameExists = await context.Groups
                        .AnyAsync(g => g.Name == name && g.Id != groupId);
                    if (nameExists)
                    {
                        return (false, "A group with this name already exists.");
                    }
                    group.Name = name;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    group.Description = description;
                }

                if (isPublic.HasValue)
                {
                    group.IsPublic = isPublic.Value;
                }

                if (isHidden.HasValue)
                {
                    group.IsHidden = isHidden.Value;
                }

                if (requiresJoinApproval.HasValue)
                {
                    group.RequiresJoinApproval = requiresJoinApproval.Value;
                }

                if (headerImageUrl != null)
                {
                    group.HeaderImageUrl = headerImageUrl;
                }

                if (verticalHeaderImageUrl != null)
                {
                    group.VerticalHeaderImageUrl = verticalHeaderImageUrl;
                }

                if (websiteUrl != null)
                {
                    group.WebsiteUrl = websiteUrl;
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Group {GroupId} updated by user {UserId}", groupId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId} by user {UserId}", groupId, userId);
                return (false, "An error occurred while updating the group.");
            }
        }

        /// <summary>
        /// Update group's last active timestamp
        /// </summary>
        public async Task<bool> UpdateLastActiveAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group != null)
                {
                    group.LastActiveAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active for group {GroupId}", groupId);
                return false;
            }
        }

        /// <summary>
        /// Update group member count
        /// </summary>
        public async Task<bool> UpdateMemberCountAsync(int groupId, int delta)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group != null)
                {
                    group.MemberCount += delta;
                    if (group.MemberCount < 0)
                    {
                        group.MemberCount = 0;
                    }
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member count for group {GroupId}", groupId);
                return false;
            }
        }

        #endregion

        #region Delete Groups

        /// <summary>
        /// Delete a group (only by creator)
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> DeleteGroupAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    return (false, "Group not found.");
                }

                // Verify user is the creator
                if (group.CreatorId != userId)
                {
                    return (false, "Only the group creator can delete the group.");
                }

                context.Groups.Remove(group);
                await context.SaveChangesAsync();

                _logger.LogInformation("Group {GroupId} deleted by creator {UserId}", groupId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {GroupId} by user {UserId}", groupId, userId);
                return (false, "An error occurred while deleting the group.");
            }
        }

        #endregion
    }
}
