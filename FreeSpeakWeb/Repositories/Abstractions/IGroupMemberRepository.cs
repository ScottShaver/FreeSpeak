using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository interface for managing group memberships
/// </summary>
public interface IGroupMemberRepository : IRepository<GroupUser>
{
    /// <summary>
    /// Get a member's membership record for a specific group
    /// </summary>
    Task<GroupUser?> GetMembershipAsync(int groupId, string userId);

    /// <summary>
    /// Get all members of a group
    /// </summary>
    Task<List<GroupUser>> GetGroupMembersAsync(int groupId, int skip = 0, int take = 50);

    /// <summary>
    /// Get all groups a user is a member of
    /// </summary>
    Task<List<GroupUser>> GetUserMembershipsAsync(string userId, int skip = 0, int take = 50);

    /// <summary>
    /// Check if a user is a member of a group
    /// </summary>
    Task<bool> IsMemberAsync(int groupId, string userId);

    /// <summary>
    /// Check if a user is an admin of a group
    /// </summary>
    Task<bool> IsAdminAsync(int groupId, string userId);

    /// <summary>
    /// Check if a user is a moderator of a group
    /// </summary>
    Task<bool> IsModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Get count of members in a group
    /// </summary>
    Task<int> GetMemberCountAsync(int groupId);

    /// <summary>
    /// Get all admin members of a group
    /// </summary>
    Task<List<GroupUser>> GetAdminsAsync(int groupId);

    /// <summary>
    /// Get all moderator members of a group
    /// </summary>
    Task<List<GroupUser>> GetModeratorsAsync(int groupId);

    /// <summary>
    /// Promote a member to admin
    /// </summary>
    Task<bool> PromoteToAdminAsync(int groupId, string userId);

    /// <summary>
    /// Demote an admin to regular member
    /// </summary>
    Task<bool> DemoteFromAdminAsync(int groupId, string userId);

    /// <summary>
    /// Promote a member to moderator
    /// </summary>
    Task<bool> PromoteToModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Demote a moderator to regular member
    /// </summary>
    Task<bool> DemoteFromModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Remove a member from a group
    /// </summary>
    Task<bool> RemoveMemberAsync(int groupId, string userId);

    /// <summary>
    /// Update member's last active timestamp
    /// </summary>
    Task UpdateLastActiveAsync(int groupId, string userId);

    /// <summary>
    /// Increment member's post count
    /// </summary>
    Task IncrementPostCountAsync(int groupId, string userId);

    /// <summary>
    /// Decrement member's post count
    /// </summary>
    Task DecrementPostCountAsync(int groupId, string userId);
}
