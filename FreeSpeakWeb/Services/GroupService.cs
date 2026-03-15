using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service providing business logic for managing groups.
    /// Handles group CRUD operations, search, suggestions, and activity tracking.
    /// </summary>
    public class GroupService
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupService> _logger;
        private readonly IAuditLogRepository _auditLogRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupService"/> class.
        /// </summary>
        /// <param name="groupRepository">Repository for group operations.</param>
        /// <param name="userRepository">Repository for user operations.</param>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        public GroupService(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupService> logger,
            IAuditLogRepository auditLogRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _contextFactory = contextFactory;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
        }

        #region Create Groups

        /// <summary>
        /// Creates a new group with the specified settings.
        /// </summary>
        /// <param name="creatorId">The unique identifier of the user creating the group.</param>
        /// <param name="name">The name of the group (must be unique).</param>
        /// <param name="description">The description of the group.</param>
        /// <param name="isPublic">Whether the group is publicly visible.</param>
        /// <param name="isHidden">Whether the group is hidden from searches.</param>
        /// <param name="requiresJoinApproval">Whether join requests require approval.</param>
        /// <param name="headerImageUrl">Optional URL for the group's header image.</param>
        /// <param name="verticalHeaderImageUrl">Optional URL for the vertical header image.</param>
        /// <param name="websiteUrl">Optional website URL for the group.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created group if successful.</returns>
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

                // Log group creation to audit log
                await _auditLogRepository.LogActionAsync(creatorId, ActionCategory.GroupAdminCreateGroup, new GroupAdminCreateGroupDetails
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Description = description?.Length > 200 ? description.Substring(0, 200) + "..." : description,
                    IsPublic = isPublic,
                    IsHidden = isHidden,
                    RequiresJoinApproval = requiresJoinApproval
                });

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
        /// Retrieves a group by its unique identifier including creator information.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The group with creator data if found; otherwise, null.</returns>
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
        /// Retrieves all public, non-hidden groups with pagination and optional search.
        /// </summary>
        /// <param name="pageSize">Number of groups per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <param name="searchTerm">Optional search term to filter by name or description.</param>
        /// <returns>A list of public groups ordered by last activity.</returns>
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
                    var lowerSearchTerm = searchTerm.ToLower();
                    query = query.Where(g => 
                        g.Name.ToLower().Contains(lowerSearchTerm) || 
                        g.Description.ToLower().Contains(lowerSearchTerm));
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
        /// Retrieves all groups created by a specific user.
        /// </summary>
        /// <param name="creatorId">The unique identifier of the creator.</param>
        /// <returns>A list of groups ordered by creation date descending.</returns>
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
        /// Gets the total count of public, non-hidden groups with optional search filter.
        /// </summary>
        /// <param name="searchTerm">Optional search term to filter by name or description.</param>
        /// <returns>The count of matching public groups.</returns>
        public async Task<int> GetPublicGroupCountAsync(string? searchTerm = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Groups
                    .Where(g => g.IsPublic && !g.IsHidden);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var lowerSearchTerm = searchTerm.ToLower();
                    query = query.Where(g => 
                        g.Name.ToLower().Contains(lowerSearchTerm) || 
                        g.Description.ToLower().Contains(lowerSearchTerm));
                }

                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public group count");
                return 0;
            }
        }

        /// <summary>
        /// Searches for public groups by partial name or description match.
        /// Excludes groups the user is already a member of or has pending requests for.
        /// </summary>
        /// <param name="searchQuery">The search query to match against group names and descriptions.</param>
        /// <param name="skip">Number of results to skip for pagination.</param>
        /// <param name="take">Number of results to return.</param>
        /// <param name="userId">Optional user ID to exclude already-joined or pending groups.</param>
        /// <returns>A list of matching groups ordered by member count and activity.</returns>
        public async Task<List<Group>> SearchGroupsAsync(string searchQuery, int skip = 0, int take = 20, string? userId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    return new List<Group>();
                }

                // Convert to lowercase for case-insensitive search
                var lowerSearchQuery = searchQuery.ToLower();

                // Get groups the user is already a member of if userId is provided
                var userGroupIds = new List<int>();
                var pendingRequestGroupIds = new List<int>();

                if (!string.IsNullOrEmpty(userId))
                {
                    // Get groups user is already a member of
                    userGroupIds = await context.GroupUsers
                        .Where(gu => gu.UserId == userId)
                        .Select(gu => gu.GroupId)
                        .ToListAsync();

                    // Get groups user has pending join requests for
                    pendingRequestGroupIds = await context.GroupJoinRequests
                        .Where(jr => jr.UserId == userId)
                        .Select(jr => jr.GroupId)
                        .ToListAsync();
                }

                // Combine both lists to exclude
                var excludedGroupIds = userGroupIds.Union(pendingRequestGroupIds).ToList();

                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.IsPublic && !g.IsHidden && 
                        (g.Name.ToLower().Contains(lowerSearchQuery) || g.Description.ToLower().Contains(lowerSearchQuery)) &&
                        !excludedGroupIds.Contains(g.Id)) // Exclude groups user is already a member of or has pending requests for
                    .OrderByDescending(g => g.MemberCount)
                    .ThenByDescending(g => g.LastActiveAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching groups with query: {SearchQuery}", searchQuery);
                return new List<Group>();
            }
        }

        /// <summary>
        /// Gets suggested groups for a user based on popularity and activity.
        /// Excludes groups the user is already a member of or has pending requests for.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="take">Maximum number of suggestions to return.</param>
        /// <returns>A list of suggested groups ordered by member count and activity.</returns>
        public async Task<List<Group>> GetSuggestedGroupsAsync(string userId, int take = 10)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get groups the user is already a member of
                var userGroupIds = await context.GroupUsers
                    .Where(gu => gu.UserId == userId)
                    .Select(gu => gu.GroupId)
                    .ToListAsync();

                // Get groups user has pending join requests for
                var pendingRequestGroupIds = await context.GroupJoinRequests
                    .Where(jr => jr.UserId == userId)
                    .Select(jr => jr.GroupId)
                    .ToListAsync();

                // Combine both lists to exclude
                var excludedGroupIds = userGroupIds.Union(pendingRequestGroupIds).ToList();

                // Get public groups the user is NOT a member of and has no pending requests for, ordered by member count and activity
                return await context.Groups
                    .Include(g => g.Creator)
                    .Where(g => g.IsPublic && !g.IsHidden && !excludedGroupIds.Contains(g.Id))
                    .OrderByDescending(g => g.MemberCount)
                    .ThenByDescending(g => g.LastActiveAt)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suggested groups for user {UserId}", userId);
                return new List<Group>();
            }
        }

        #endregion

        #region Update Groups

        /// <summary>
        /// Updates group information. Only the group creator can perform this action.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="userId">The user ID attempting the update (must be creator).</param>
        /// <param name="name">Optional new name for the group.</param>
        /// <param name="description">Optional new description.</param>
        /// <param name="isPublic">Optional new public visibility setting.</param>
        /// <param name="isHidden">Optional new hidden setting.</param>
        /// <param name="requiresJoinApproval">Optional new join approval requirement.</param>
        /// <param name="enablePointsSystem">Optional setting to enable or disable the points system for this group.</param>
        /// <param name="headerImageUrl">Optional new header image URL.</param>
        /// <param name="verticalHeaderImageUrl">Optional new vertical header image URL.</param>
        /// <param name="websiteUrl">Optional new website URL.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdateGroupAsync(
            int groupId,
            string userId,
            string? name = null,
            string? description = null,
            bool? isPublic = null,
            bool? isHidden = null,
            bool? requiresJoinApproval = null,
            bool? enablePointsSystem = null,
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

                // Track changes for audit log
                var changedFields = new List<string>();
                var oldName = group.Name;
                var oldIsPublic = group.IsPublic;

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
                    if (group.Name != name)
                    {
                        changedFields.Add("Name");
                    }
                    group.Name = name;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    if (group.Description != description)
                    {
                        changedFields.Add("Description");
                    }
                    group.Description = description;
                }

                if (isPublic.HasValue)
                {
                    if (group.IsPublic != isPublic.Value)
                    {
                        changedFields.Add("IsPublic");
                    }
                    group.IsPublic = isPublic.Value;
                }

                if (isHidden.HasValue)
                {
                    if (group.IsHidden != isHidden.Value)
                    {
                        changedFields.Add("IsHidden");
                    }
                    group.IsHidden = isHidden.Value;
                }

                if (requiresJoinApproval.HasValue)
                {
                    if (group.RequiresJoinApproval != requiresJoinApproval.Value)
                    {
                        changedFields.Add("RequiresJoinApproval");
                    }
                    group.RequiresJoinApproval = requiresJoinApproval.Value;
                }

                if (enablePointsSystem.HasValue)
                {
                    if (group.EnablePointsSystem != enablePointsSystem.Value)
                    {
                        changedFields.Add("EnablePointsSystem");
                    }
                    group.EnablePointsSystem = enablePointsSystem.Value;
                }

                if (headerImageUrl != null)
                {
                    if (group.HeaderImageUrl != headerImageUrl)
                    {
                        changedFields.Add("HeaderImageUrl");
                    }
                    group.HeaderImageUrl = headerImageUrl;
                }

                if (verticalHeaderImageUrl != null)
                {
                    if (group.VerticalHeaderImageUrl != verticalHeaderImageUrl)
                    {
                        changedFields.Add("VerticalHeaderImageUrl");
                    }
                    group.VerticalHeaderImageUrl = verticalHeaderImageUrl;
                }

                if (websiteUrl != null)
                {
                    if (group.WebsiteUrl != websiteUrl)
                    {
                        changedFields.Add("WebsiteUrl");
                    }
                    group.WebsiteUrl = websiteUrl;
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Group {GroupId} updated by user {UserId}", groupId, userId);

                // Log group edit to audit log if changes were made
                if (changedFields.Count > 0)
                {
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.GroupAdminEditGroup, new GroupAdminEditGroupDetails
                    {
                        GroupId = groupId,
                        GroupName = group.Name,
                        ChangedFields = changedFields,
                        OldName = changedFields.Contains("Name") ? oldName : null,
                        NewName = changedFields.Contains("Name") ? group.Name : null,
                        OldIsPublic = changedFields.Contains("IsPublic") ? oldIsPublic : null,
                        NewIsPublic = changedFields.Contains("IsPublic") ? group.IsPublic : null
                    });
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId} by user {UserId}", groupId, userId);
                return (false, "An error occurred while updating the group.");
            }
        }

        /// <summary>
        /// Updates a group's last active timestamp to the current time.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
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
        /// Updates the group's member count by the specified delta.
        /// Ensures the count does not go below zero.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="delta">The amount to change the member count (positive or negative).</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
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
        /// Deletes a group. Only the group creator can perform this action.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group to delete.</param>
        /// <param name="userId">The user ID attempting the deletion (must be creator).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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

                // Capture details before deletion for audit log
                var groupName = group.Name;
                var memberCount = group.MemberCount;

                context.Groups.Remove(group);
                await context.SaveChangesAsync();

                // Log group deletion to audit log
                await _auditLogRepository.LogActionAsync(userId, ActionCategory.GroupAdminCloseGroup, new GroupAdminCloseGroupDetails
                {
                    GroupId = groupId,
                    GroupName = groupName,
                    MemberCountAtClosure = memberCount,
                    CloseType = "Deleted",
                    Reason = "Group deleted by creator"
                });

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
