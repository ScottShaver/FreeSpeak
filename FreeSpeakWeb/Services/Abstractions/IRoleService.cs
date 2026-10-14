namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Service interface for managing user roles in ASP.NET Core Identity.
    /// Provides methods for retrieving, adding, and removing roles from users.
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// Retrieves all available roles in the system.
        /// </summary>
        /// <returns>A list of role names.</returns>
        Task<List<string>> GetAllRolesAsync();

        /// <summary>
        /// Retrieves all roles assigned to a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of role names assigned to the user.</returns>
        Task<List<string>> GetUserRolesAsync(string userId);

        /// <summary>
        /// Adds a user to a specific role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to add.</param>
        /// <param name="actorUserId">The identifier of the user performing the change (recorded in the audit log).</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> AddUserToRoleAsync(string userId, string roleName, string actorUserId);

        /// <summary>
        /// Removes a user from a specific role.
        /// Removing the SystemAdministrator role from the last remaining system administrator is refused.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to remove.</param>
        /// <param name="actorUserId">The identifier of the user performing the change (recorded in the audit log).</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> RemoveUserFromRoleAsync(string userId, string roleName, string actorUserId);

        /// <summary>
        /// Determines whether the given user is the only remaining system administrator.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is a system administrator and no other system administrator exists.</returns>
        Task<bool> IsLastSystemAdministratorAsync(string userId);

        /// <summary>
        /// Checks if a role exists in the system.
        /// </summary>
        /// <param name="roleName">The name of the role to check.</param>
        /// <returns>True if the role exists; otherwise, false.</returns>
        Task<bool> RoleExistsAsync(string roleName);

        /// <summary>
        /// Checks if a user is in a specific role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to check.</param>
        /// <returns>True if the user is in the role; otherwise, false.</returns>
        Task<bool> IsUserInRoleAsync(string userId, string roleName);

        /// <summary>
        /// Checks if a user is a system administrator.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is a system administrator; otherwise, false.</returns>
        Task<bool> IsSystemAdministratorAsync(string userId);
    }
}
