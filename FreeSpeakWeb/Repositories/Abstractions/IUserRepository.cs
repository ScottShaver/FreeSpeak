using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for ApplicationUser entity
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Get a user by ID
        /// </summary>
        Task<ApplicationUser?> GetByIdAsync(string userId);

        /// <summary>
        /// Get users by IDs
        /// </summary>
        Task<List<ApplicationUser>> GetByIdsAsync(IEnumerable<string> userIds);

        /// <summary>
        /// Get a user by username
        /// </summary>
        Task<ApplicationUser?> GetByUsernameAsync(string username);

        /// <summary>
        /// Get a user by email
        /// </summary>
        Task<ApplicationUser?> GetByEmailAsync(string email);

        /// <summary>
        /// Search users by display name or username
        /// </summary>
        Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 50);

        /// <summary>
        /// Update user profile
        /// </summary>
        Task<bool> UpdateProfileAsync(ApplicationUser user);

        /// <summary>
        /// Update user's profile picture URL
        /// </summary>
        Task<bool> UpdateProfilePictureAsync(string userId, string profilePictureUrl);

        /// <summary>
        /// Check if a username is available
        /// </summary>
        Task<bool> IsUsernameAvailableAsync(string username, string? excludeUserId = null);

        /// <summary>
        /// Check if an email is available
        /// </summary>
        Task<bool> IsEmailAvailableAsync(string email, string? excludeUserId = null);

        /// <summary>
        /// Check if a user exists
        /// </summary>
        Task<bool> ExistsAsync(string userId);
    }
}
