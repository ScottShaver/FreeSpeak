using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing ApplicationUser entities.
    /// Provides methods for user retrieval, profile updates, and availability checks.
    /// Integrates with ASP.NET Core Identity for authentication and authorization.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user if found; otherwise, null.</returns>
        Task<ApplicationUser?> GetByIdAsync(string userId);

        /// <summary>
        /// Retrieves multiple users by their unique identifiers in a single query.
        /// Efficient for batch operations and avoiding N+1 query problems.
        /// </summary>
        /// <param name="userIds">Collection of user identifiers to retrieve.</param>
        /// <returns>A list of users matching the provided IDs.</returns>
        Task<List<ApplicationUser>> GetByIdsAsync(IEnumerable<string> userIds);

        /// <summary>
        /// Retrieves a user by their username.
        /// </summary>
        /// <param name="username">The username to search for.</param>
        /// <returns>The user if found; otherwise, null.</returns>
        Task<ApplicationUser?> GetByUsernameAsync(string username);

        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address to search for.</param>
        /// <returns>The user if found; otherwise, null.</returns>
        Task<ApplicationUser?> GetByEmailAsync(string email);

        /// <summary>
        /// Searches for users by display name or username with pagination support.
        /// Useful for user discovery and friend search features.
        /// </summary>
        /// <param name="searchTerm">The search term to match against names and usernames.</param>
        /// <param name="skip">Number of results to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of results to retrieve. Default is 50.</param>
        /// <returns>A list of users matching the search criteria.</returns>
        Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 50);

        /// <summary>
        /// Updates a user's profile information.
        /// </summary>
        /// <param name="user">The user entity with updated profile data.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        Task<bool> UpdateProfileAsync(ApplicationUser user);

        /// <summary>
        /// Updates a user's profile picture URL.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="profilePictureUrl">The new profile picture URL or path.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        Task<bool> UpdateProfilePictureAsync(string userId, string profilePictureUrl);

        /// <summary>
        /// Checks whether a username is available for use.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check (for current user updating their own username).</param>
        /// <returns>True if the username is available; otherwise, false.</returns>
        Task<bool> IsUsernameAvailableAsync(string username, string? excludeUserId = null);

        /// <summary>
        /// Checks whether an email address is available for use.
        /// </summary>
        /// <param name="email">The email address to check.</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check (for current user updating their own email).</param>
        /// <returns>True if the email is available; otherwise, false.</returns>
        Task<bool> IsEmailAvailableAsync(string email, string? excludeUserId = null);

        /// <summary>
        /// Checks whether a user with the specified ID exists.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <returns>True if the user exists; otherwise, false.</returns>
        Task<bool> ExistsAsync(string userId);
    }
}
