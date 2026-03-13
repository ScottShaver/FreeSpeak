using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository interface for managing group memberships (GroupUser entities).
/// Handles member roles (admin, moderator, regular member), permissions, activity tracking,
/// and member management operations within groups.
/// </summary>
public interface IGroupMemberRepository : IRepository<GroupUser>
{
    /// <summary>
    /// Retrieves a user's membership record for a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The membership record if found; otherwise, null.</returns>
    Task<GroupUser?> GetMembershipAsync(int groupId, string userId);

    /// <summary>
    /// Retrieves all members of a group with pagination support.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="skip">Number of members to skip for pagination. Default is 0.</param>
    /// <param name="take">Number of members to retrieve. Default is 50.</param>
    /// <returns>A list of group members.</returns>
    Task<List<GroupUser>> GetGroupMembersAsync(int groupId, int skip = 0, int take = 50);

    /// <summary>
    /// Retrieves all groups that a user is a member of with pagination.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="skip">Number of memberships to skip for pagination. Default is 0.</param>
    /// <param name="take">Number of memberships to retrieve. Default is 50.</param>
    /// <returns>A list of group memberships for the user.</returns>
    Task<List<GroupUser>> GetUserMembershipsAsync(string userId, int skip = 0, int take = 50);

    /// <summary>
    /// Checks whether a user is a member of a specific group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user is a member; otherwise, false.</returns>
    Task<bool> IsMemberAsync(int groupId, string userId);

    /// <summary>
    /// Checks whether a user has administrator privileges in a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user is an admin; otherwise, false.</returns>
    Task<bool> IsAdminAsync(int groupId, string userId);

    /// <summary>
    /// Checks whether a user has moderator privileges in a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user is a moderator; otherwise, false.</returns>
    Task<bool> IsModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Gets the total count of members in a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <returns>The number of members in the group.</returns>
    Task<int> GetMemberCountAsync(int groupId);

    /// <summary>
    /// Retrieves all administrator members of a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <returns>A list of admin members.</returns>
    Task<List<GroupUser>> GetAdminsAsync(int groupId);

    /// <summary>
    /// Retrieves all moderator members of a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <returns>A list of moderator members.</returns>
    Task<List<GroupUser>> GetModeratorsAsync(int groupId);

    /// <summary>
    /// Promotes a member to administrator status.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user to promote.</param>
    /// <returns>True if the promotion was successful; otherwise, false.</returns>
    Task<bool> PromoteToAdminAsync(int groupId, string userId);

    /// <summary>
    /// Demotes an administrator to regular member status.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user to demote.</param>
    /// <returns>True if the demotion was successful; otherwise, false.</returns>
    Task<bool> DemoteFromAdminAsync(int groupId, string userId);

    /// <summary>
    /// Promotes a member to moderator status.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user to promote.</param>
    /// <returns>True if the promotion was successful; otherwise, false.</returns>
    Task<bool> PromoteToModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Demotes a moderator to regular member status.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user to demote.</param>
    /// <returns>True if the demotion was successful; otherwise, false.</returns>
    Task<bool> DemoteFromModeratorAsync(int groupId, string userId);

    /// <summary>
    /// Removes a member from a group entirely.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user to remove.</param>
    /// <returns>True if the member was successfully removed; otherwise, false.</returns>
    Task<bool> RemoveMemberAsync(int groupId, string userId);

    /// <summary>
    /// Updates the LastActiveAt timestamp for a member's activity in a group.
    /// Called when the member posts, comments, or interacts within the group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateLastActiveAsync(int groupId, string userId);

    /// <summary>
    /// Increments the post count for a member in a group.
    /// Called when the member creates a new post in the group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementPostCountAsync(int groupId, string userId);

    /// <summary>
    /// Decrements the post count for a member in a group.
    /// Called when the member deletes a post from the group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DecrementPostCountAsync(int groupId, string userId);
}
