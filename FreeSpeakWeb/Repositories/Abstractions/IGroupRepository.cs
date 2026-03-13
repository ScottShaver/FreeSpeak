using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing Group entities.
    /// Provides methods for group CRUD operations, member queries, search, and permission checks.
    /// Supports public group discovery and user-specific group listings.
    /// </summary>
    public interface IGroupRepository : IRepository<Group>
    {
        /// <summary>
        /// Retrieves a group by its unique identifier with optional related data.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="includeMembers">Whether to include the group members collection. Default is false.</param>
        /// <param name="includeRules">Whether to include the group rules collection. Default is false.</param>
        /// <returns>The group if found; otherwise, null.</returns>
        Task<Group?> GetByIdAsync(int groupId, bool includeMembers = false, bool includeRules = false);

        /// <summary>
        /// Retrieves all public groups with pagination support.
        /// Returns only groups where IsPublic is true and IsHidden is false.
        /// </summary>
        /// <param name="skip">Number of groups to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of groups to retrieve. Default is 50.</param>
        /// <returns>A list of public groups, typically ordered by activity or member count.</returns>
        Task<List<Group>> GetPublicGroupsAsync(int skip = 0, int take = 50);

        /// <summary>
        /// Retrieves groups that a user is a member of with pagination.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="skip">Number of groups to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of groups to retrieve. Default is 50.</param>
        /// <returns>A list of groups the user has joined.</returns>
        Task<List<Group>> GetUserGroupsAsync(string userId, int skip = 0, int take = 50);

        /// <summary>
        /// Retrieves groups created by a specific user with pagination.
        /// </summary>
        /// <param name="userId">The ID of the user who created the groups.</param>
        /// <param name="skip">Number of groups to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of groups to retrieve. Default is 50.</param>
        /// <returns>A list of groups created by the specified user.</returns>
        Task<List<Group>> GetGroupsCreatedByUserAsync(string userId, int skip = 0, int take = 50);

        /// <summary>
        /// Searches for groups by name or description with pagination.
        /// Useful for group discovery features.
        /// </summary>
        /// <param name="searchTerm">The search term to match against group names and descriptions.</param>
        /// <param name="skip">Number of results to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of results to retrieve. Default is 50.</param>
        /// <returns>A list of groups matching the search criteria.</returns>
        Task<List<Group>> SearchGroupsAsync(string searchTerm, int skip = 0, int take = 50);

        /// <summary>
        /// Checks whether a user is a member of a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is a member of the group; otherwise, false.</returns>
        Task<bool> IsUserMemberAsync(int groupId, string userId);

        /// <summary>
        /// Checks whether a user is the creator of a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user created the group; otherwise, false.</returns>
        Task<bool> IsUserCreatorAsync(int groupId, string userId);

        /// <summary>
        /// Checks whether a user has administrator privileges in a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is an admin of the group; otherwise, false.</returns>
        Task<bool> IsUserAdminAsync(int groupId, string userId);

        /// <summary>
        /// Gets the total member count for a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The number of members in the group.</returns>
        Task<int> GetMemberCountAsync(int groupId);

        /// <summary>
        /// Updates the LastActiveAt timestamp for a group.
        /// Called when new activity occurs in the group (posts, comments, etc.).
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateLastActiveAsync(int groupId);
    }
}
