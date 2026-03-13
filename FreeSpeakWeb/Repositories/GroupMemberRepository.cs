using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories;

/// <summary>
/// Implementation of IGroupMemberRepository for managing group memberships
/// </summary>
public class GroupMemberRepository : IGroupMemberRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<GroupMemberRepository> _logger;

    public GroupMemberRepository(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<GroupMemberRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<GroupUser?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Include(gu => gu.Group)
            .FirstOrDefaultAsync(gu => gu.Id == id);
    }

    public async Task<List<GroupUser>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Include(gu => gu.Group)
            .ToListAsync();
    }

    public async Task<GroupUser> AddAsync(GroupUser entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Add(entity);
        await context.SaveChangesAsync();

        // Update group member count
        await context.Groups
            .Where(g => g.Id == entity.GroupId)
            .ExecuteUpdateAsync(g => g.SetProperty(x => x.MemberCount, x => x.MemberCount + 1));

        return entity;
    }

    public async Task UpdateAsync(GroupUser entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(GroupUser entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.GroupUsers.Remove(entity);
        await context.SaveChangesAsync();

        // Update group member count
        await context.Groups
            .Where(g => g.Id == entity.GroupId)
            .ExecuteUpdateAsync(g => g.SetProperty(x => x.MemberCount, x => Math.Max(0, x.MemberCount - 1)));
    }

    public async Task<bool> ExistsAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.AnyAsync(gu => gu.Id == id);
    }

    public async Task<GroupUser?> GetMembershipAsync(int groupId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
    }

    public async Task<List<GroupUser>> GetGroupMembersAsync(int groupId, int skip = 0, int take = 50)
    {
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

    public async Task<List<GroupUser>> GetUserMembershipsAsync(string userId, int skip = 0, int take = 50)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.Group)
            .Where(gu => gu.UserId == userId)
            .OrderByDescending(gu => gu.LastActiveAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> IsMemberAsync(int groupId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
    }

    public async Task<bool> IsAdminAsync(int groupId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var membership = await context.GroupUsers
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
        return membership?.IsAdmin == true;
    }

    public async Task<bool> IsModeratorAsync(int groupId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var membership = await context.GroupUsers
            .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
        return membership?.IsModerator == true;
    }

    public async Task<int> GetMemberCountAsync(int groupId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers.CountAsync(gu => gu.GroupId == groupId);
    }

    public async Task<List<GroupUser>> GetAdminsAsync(int groupId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Where(gu => gu.GroupId == groupId && gu.IsAdmin)
            .ToListAsync();
    }

    public async Task<List<GroupUser>> GetModeratorsAsync(int groupId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupUsers
            .Include(gu => gu.User)
            .Where(gu => gu.GroupId == groupId && gu.IsModerator)
            .ToListAsync();
    }

    public async Task<bool> PromoteToAdminAsync(int groupId, string userId)
    {
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

    public async Task<bool> DemoteFromAdminAsync(int groupId, string userId)
    {
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

    public async Task<bool> PromoteToModeratorAsync(int groupId, string userId)
    {
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

    public async Task<bool> DemoteFromModeratorAsync(int groupId, string userId)
    {
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

    public async Task<bool> RemoveMemberAsync(int groupId, string userId)
    {
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

    public async Task UpdateLastActiveAsync(int groupId, string userId)
    {
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

    public async Task IncrementPostCountAsync(int groupId, string userId)
    {
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

    public async Task DecrementPostCountAsync(int groupId, string userId)
    {
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
