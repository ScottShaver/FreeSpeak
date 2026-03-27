using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories;

/// <summary>
/// Repository implementation for managing group memberships.
/// Provides operations for adding, removing, and querying group members,
/// as well as managing admin and moderator roles.
/// </summary>
public class GroupMemberRepository : IGroupMemberRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<GroupMemberRepository> _logger;
    private readonly ProfilerHelper _profiler;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupMemberRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory for creating database contexts.</param>
    /// <param name="logger">Logger for recording repository operations.</param>
    /// <param name="profiler">Helper for profiling repository operations.</param>
    public GroupMemberRepository(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<GroupMemberRepository> logger,
        ProfilerHelper profiler)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _profiler = profiler;
    }

    /// <summary>
    /// Retrieves a group membership by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the membership.</param>
    /// <returns>The group membership with user and group details if found; otherwise, null.</returns>
    public async Task<GroupUser?> GetByIdAsync(int id)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetByIdAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Include(gu => gu.Group)
            .FirstOrDefaultAsync(gu => gu.Id == id);
    }

    /// <summary>
    /// Retrieves all group memberships in the system.
    /// </summary>
    /// <returns>A list of all group memberships with user and group details.</returns>
    public async Task<List<GroupUser>> GetAllAsync()
    {
        using var step = _profiler.Step("GroupMemberRepository.GetAllAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Include(gu => gu.Group)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new group membership and increments the group's member count.
    /// </summary>
    /// <param name="entity">The group membership entity to add.</param>
    /// <returns>The added group membership entity.</returns>
    public async Task<GroupUser> AddAsync(GroupUser entity)
    {
        using var step = _profiler.Step("GroupMemberRepository.AddAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Add(entity);
        await context.SaveChangesAsync();

        // Update group member count
        await context.Groups
            .Where(g => g.Id == entity.GroupId)
            .ExecuteUpdateAsync(g => g.SetProperty(x => x.MemberCount, x => x.MemberCount + 1));

        return entity;
    }

    /// <summary>
    /// Updates an existing group membership entity.
    /// </summary>
    /// <param name="entity">The group membership entity with updated values.</param>
    public async Task UpdateAsync(GroupUser entity)
    {
        using var step = _profiler.Step($"GroupMemberRepository.UpdateAsync({entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Update(entity);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a group membership and decrements the group's member count.
    /// </summary>
    /// <param name="entity">The group membership entity to delete.</param>
    public async Task DeleteAsync(GroupUser entity)
    {
        using var step = _profiler.Step($"GroupMemberRepository.DeleteAsync({entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Remove(entity);
        await context.SaveChangesAsync();

        // Update group member count
        await context.Groups
            .Where(g => g.Id == entity.GroupId)
            .ExecuteUpdateAsync(g => g.SetProperty(x => x.MemberCount, x => Math.Max(0, x.MemberCount - 1)));
    }

    /// <summary>
    /// Checks whether a group membership with the specified ID exists.
    /// </summary>
    /// <param name="id">The unique identifier of the membership.</param>
    /// <returns>True if the membership exists; otherwise, false.</returns>
    public async Task<bool> ExistsAsync(int id)
    {
        using var step = _profiler.Step($"GroupMemberRepository.ExistsAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.AnyAsync(gu => gu.Id == id);
    }

    /// <summary>
    /// Retrieves a user's membership in a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The group membership if found; otherwise, null.</returns>
    public async Task<GroupUser?> GetMembershipAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetMembershipAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
    }

    /// <summary>
    /// Retrieves members of a group with pagination, ordered by role (admins first, then moderators).
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="skip">Number of members to skip for pagination.</param>
    /// <param name="take">Number of members to return.</param>
    /// <returns>A list of group memberships with user details.</returns>
    public async Task<List<GroupUser>> GetGroupMembersAsync(int groupId, int skip = 0, int take = 50)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetGroupMembersAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Where(gu => gu.GroupId == groupId)
            .OrderByDescending(gu => gu.IsAdmin)
            .ThenByDescending(gu => gu.IsModerator)
            .ThenBy(gu => gu.JoinedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all group memberships for a user with pagination.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="skip">Number of memberships to skip for pagination.</param>
    /// <param name="take">Number of memberships to return.</param>
    /// <returns>A list of group memberships ordered by last activity descending.</returns>
    public async Task<List<GroupUser>> GetUserMembershipsAsync(string userId, int skip = 0, int take = 50)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetUserMembershipsAsync({userId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.Group)
            .Where(gu => gu.UserId == userId)
            .OrderByDescending(gu => gu.LastActiveAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Checks whether a user is a member of a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the user is a member; otherwise, false.</returns>
    public async Task<bool> IsMemberAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.IsMemberAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
    }

    /// <summary>
    /// Checks whether a user is an administrator of a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the user is an admin; otherwise, false.</returns>
    public async Task<bool> IsAdminAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.IsAdminAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        var membership = await context.GroupUsers
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
        return membership?.IsAdmin == true;
    }

    /// <summary>
    /// Checks whether a user is a moderator of a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the user is a moderator; otherwise, false.</returns>
    public async Task<bool> IsModeratorAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.IsModeratorAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        var membership = await context.GroupUsers
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
        return membership?.IsModerator == true;
    }

    /// <summary>
    /// Gets the total count of members in a group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>The number of members in the group.</returns>
    public async Task<int> GetMemberCountAsync(int groupId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetMemberCountAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.CountAsync(gu => gu.GroupId == groupId);
    }

    /// <summary>
    /// Retrieves all administrators of a group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>A list of group memberships for users with admin privileges.</returns>
    public async Task<List<GroupUser>> GetAdminsAsync(int groupId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetAdminsAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Where(gu => gu.GroupId == groupId && gu.IsAdmin)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all moderators of a group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>A list of group memberships for users with moderator privileges.</returns>
    public async Task<List<GroupUser>> GetModeratorsAsync(int groupId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.GetModeratorsAsync({groupId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Where(gu => gu.GroupId == groupId && gu.IsModerator)
            .ToListAsync();
    }

    /// <summary>
    /// Promotes a group member to administrator status.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user to promote.</param>
    /// <returns>True if the promotion was successful; otherwise, false.</returns>
    public async Task<bool> PromoteToAdminAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.PromoteToAdminAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var membership = await context.GroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (membership == null) return false;

            membership.IsAdmin = true;
            await context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} promoted to admin in group {GroupId}", userId, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting user {UserId} to admin in group {GroupId}", userId, groupId);
            return false;
        }
    }

    /// <summary>
    /// Demotes a group administrator to regular member status.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user to demote.</param>
    /// <returns>True if the demotion was successful; otherwise, false.</returns>
    public async Task<bool> DemoteFromAdminAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.DemoteFromAdminAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var membership = await context.GroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (membership == null) return false;

            membership.IsAdmin = false;
            await context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} demoted from admin in group {GroupId}", userId, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error demoting user {UserId} from admin in group {GroupId}", userId, groupId);
            return false;
        }
    }

    /// <summary>
    /// Promotes a group member to moderator status.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user to promote.</param>
    /// <returns>True if the promotion was successful; otherwise, false.</returns>
    public async Task<bool> PromoteToModeratorAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.PromoteToModeratorAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var membership = await context.GroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (membership == null) return false;

            membership.IsModerator = true;
            await context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} promoted to moderator in group {GroupId}", userId, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error promoting user {UserId} to moderator in group {GroupId}", userId, groupId);
            return false;
        }
    }

    /// <summary>
    /// Demotes a group moderator to regular member status.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user to demote.</param>
    /// <returns>True if the demotion was successful; otherwise, false.</returns>
    public async Task<bool> DemoteFromModeratorAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.DemoteFromModeratorAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var membership = await context.GroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (membership == null) return false;

            membership.IsModerator = false;
            await context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} demoted from moderator in group {GroupId}", userId, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error demoting user {UserId} from moderator in group {GroupId}", userId, groupId);
            return false;
        }
    }

    /// <summary>
    /// Removes a member from a group and decrements the group's member count.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user to remove.</param>
    /// <returns>True if the removal was successful; otherwise, false.</returns>
    public async Task<bool> RemoveMemberAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.RemoveMemberAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var membership = await context.GroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (membership == null) return false;

            context.GroupUsers.Remove(membership);
            await context.SaveChangesAsync();

            // Update group member count
            await context.Groups
                .Where(g => g.Id == groupId)
                .ExecuteUpdateAsync(g => g.SetProperty(x => x.MemberCount, x => Math.Max(0, x.MemberCount - 1)));

            _logger.LogInformation("User {UserId} removed from group {GroupId}", userId, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
            return false;
        }
    }

    /// <summary>
    /// Updates the last active timestamp for a user's membership in a group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    public async Task UpdateLastActiveAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.UpdateLastActiveAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.GroupUsers
                .Where(gu => gu.GroupId == groupId && gu.UserId == userId)
                .ExecuteUpdateAsync(gu => gu.SetProperty(x => x.LastActiveAt, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last active for user {UserId} in group {GroupId}", userId, groupId);
        }
    }

    /// <summary>
    /// Increments the post count for a user's membership in a group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    public async Task IncrementPostCountAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.IncrementPostCountAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.GroupUsers
                .Where(gu => gu.GroupId == groupId && gu.UserId == userId)
                .ExecuteUpdateAsync(gu => gu.SetProperty(x => x.PostCount, x => x.PostCount + 1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing post count for user {UserId} in group {GroupId}", userId, groupId);
        }
    }

    /// <summary>
    /// Decrements the post count for a user's membership in a group, with a minimum of zero.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    public async Task DecrementPostCountAsync(int groupId, string userId)
    {
        using var step = _profiler.Step($"GroupMemberRepository.DecrementPostCountAsync({groupId})");
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.GroupUsers
                .Where(gu => gu.GroupId == groupId && gu.UserId == userId)
                .ExecuteUpdateAsync(gu => gu.SetProperty(x => x.PostCount, x => Math.Max(0, x.PostCount - 1)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrementing post count for user {UserId} in group {GroupId}", userId, groupId);
        }
    }
}
