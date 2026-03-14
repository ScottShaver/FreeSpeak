using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for ApplicationUser entities.
    /// Provides operations for retrieving, searching, and updating user profiles.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<UserRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public UserRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<UserRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user entity if found; otherwise, null.</returns>
        public async Task<ApplicationUser?> GetByIdAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Users.FindAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Retrieves multiple users by their unique identifiers in a single query.
        /// </summary>
        /// <param name="userIds">Collection of user IDs to retrieve.</param>
        /// <returns>A list of users matching the provided IDs.</returns>
        public async Task<List<ApplicationUser>> GetByIdsAsync(IEnumerable<string> userIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var userIdList = userIds.ToList();
                return await context.Users.Where(u => userIdList.Contains(u.Id)).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving multiple users");
                return new List<ApplicationUser>();
            }
        }

        /// <summary>
        /// Retrieves a user by their username (case-insensitive).
        /// </summary>
        /// <param name="username">The username to search for.</param>
        /// <returns>The user entity if found; otherwise, null.</returns>
        public async Task<ApplicationUser?> GetByUsernameAsync(string username)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Users
                    .FirstOrDefaultAsync(u => u.UserName != null && u.UserName.ToLower() == username.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Retrieves a user by their email address (case-insensitive).
        /// </summary>
        /// <param name="email">The email address to search for.</param>
        /// <returns>The user entity if found; otherwise, null.</returns>
        public async Task<ApplicationUser?> GetByEmailAsync(string email)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Users
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email {Email}", email);
                return null;
            }
        }

        /// <summary>
        /// Searches for users by first name, last name, or username.
        /// </summary>
        /// <param name="searchTerm">The term to search for (case-insensitive).</param>
        /// <param name="skip">Number of users to skip for pagination.</param>
        /// <param name="take">Number of users to return.</param>
        /// <returns>A list of matching users ordered by first name.</returns>
        public async Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var searchLower = searchTerm.ToLower();

                return await context.Users
                    .Where(u =>
                        (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                        (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(searchLower)))
                    .OrderBy(u => u.FirstName)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term {SearchTerm}", searchTerm);
                return new List<ApplicationUser>();
            }
        }

        /// <summary>
        /// Searches for users across all profile fields for administrative purposes.
        /// Searches FirstName, LastName, UserName, Email, City, State, and Occupation.
        /// </summary>
        /// <param name="searchTerm">The term to search for (case-insensitive).</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>A list of matching users ordered by first name.</returns>
        public async Task<List<ApplicationUser>> AdminSearchUsersAsync(string searchTerm, int maxResults = 100)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var searchLower = searchTerm.ToLower();

                return await context.Users
                    .Where(u =>
                        (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                        (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                        (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                        (u.City != null && u.City.ToLower().Contains(searchLower)) ||
                        (u.State != null && u.State.ToLower().Contains(searchLower)) ||
                        (u.Occupation != null && u.Occupation.ToLower().Contains(searchLower)))
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Take(maxResults)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing admin search with term {SearchTerm}", searchTerm);
                return new List<ApplicationUser>();
            }
        }

        /// <summary>
        /// Updates a user's profile information.
        /// </summary>
        /// <param name="user">The user entity with updated values.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> UpdateProfileAsync(ApplicationUser user)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Users.Update(user);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", user.Id);
                return false;
            }
        }

        /// <summary>
        /// Updates a user's profile picture URL.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="profilePictureUrl">The new profile picture URL.</param>
        /// <returns>True if the update was successful; otherwise, false.</returns>
        public async Task<bool> UpdateProfilePictureAsync(string userId, string profilePictureUrl)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var user = await context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                user.ProfilePictureUrl = profilePictureUrl;
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile picture for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Checks whether a username is available for use.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check (for updating own username).</param>
        /// <returns>True if the username is available; otherwise, false.</returns>
        public async Task<bool> IsUsernameAvailableAsync(string username, string? excludeUserId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.Users
                    .Where(u => u.UserName != null && u.UserName.ToLower() == username.ToLower());

                if (excludeUserId != null)
                    query = query.Where(u => u.Id != excludeUserId);

                return !await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking username availability for {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Checks whether an email address is available for use.
        /// </summary>
        /// <param name="email">The email address to check.</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check (for updating own email).</param>
        /// <returns>True if the email is available; otherwise, false.</returns>
        public async Task<bool> IsEmailAvailableAsync(string email, string? excludeUserId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.Users
                    .Where(u => u.Email != null && u.Email.ToLower() == email.ToLower());

                if (excludeUserId != null)
                    query = query.Where(u => u.Id != excludeUserId);

                return !await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email availability for {Email}", email);
                return false;
            }
        }

        /// <summary>
        /// Checks whether a user with the specified ID exists.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Users.AnyAsync(u => u.Id == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of user {UserId}", userId);
                return false;
            }
        }
    }
}
