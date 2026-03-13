using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for ApplicationUser entity
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<UserRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

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
