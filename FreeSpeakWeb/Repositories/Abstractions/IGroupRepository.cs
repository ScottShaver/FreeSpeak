using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for Group entity
    /// </summary>
    public interface IGroupRepository : IRepository<Group>
    {
        /// <summary>
        /// Get a group by ID with optional includes
        /// </summary>
        Task<Group?> GetByIdAsync(int groupId, bool includeMembers = false, bool includeRules = false);

        /// <summary>
        /// Get all public groups
        /// </summary>
        Task<List<Group>> GetPublicGroupsAsync(int skip = 0, int take = 50);

        /// <summary>
        /// Get groups a user is a member of
        /// </summary>
        Task<List<Group>> GetUserGroupsAsync(string userId, int skip = 0, int take = 50);

        /// <summary>
        /// Get groups created by a user
        /// </summary>
        Task<List<Group>> GetGroupsCreatedByUserAsync(string userId, int skip = 0, int take = 50);

        /// <summary>
        /// Search groups by name or description
        /// </summary>
        Task<List<Group>> SearchGroupsAsync(string searchTerm, int skip = 0, int take = 50);

        /// <summary>
        /// Check if a user is a member of a group
        /// </summary>
        Task<bool> IsUserMemberAsync(int groupId, string userId);

        /// <summary>
        /// Check if a user is the creator of a group
        /// </summary>
        Task<bool> IsUserCreatorAsync(int groupId, string userId);

        /// <summary>
        /// Check if a user is an admin of a group
        /// </summary>
        Task<bool> IsUserAdminAsync(int groupId, string userId);

        /// <summary>
        /// Get member count for a group
        /// </summary>
        Task<int> GetMemberCountAsync(int groupId);

        /// <summary>
        /// Update group's last active timestamp
        /// </summary>
        Task UpdateLastActiveAsync(int groupId);
    }
}
