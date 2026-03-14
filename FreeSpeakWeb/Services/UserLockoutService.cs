using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service implementation for managing user account lockouts in ASP.NET Core Identity.
    /// Provides functionality to set, remove, and query lockout status for user accounts.
    /// </summary>
    public class UserLockoutService : IUserLockoutService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserLockoutService> _logger;

        /// <summary>
        /// Initializes a new instance of the UserLockoutService class.
        /// </summary>
        /// <param name="userManager">The UserManager for managing users.</param>
        /// <param name="logger">Logger for recording lockout operations and errors.</param>
        public UserLockoutService(
            UserManager<ApplicationUser> userManager,
            ILogger<UserLockoutService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current lockout status for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A lockout status object containing lockout end time and failed access count.</returns>
        public async Task<UserLockoutStatus> GetLockoutStatusAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return new UserLockoutStatus
                    {
                        LockoutEnd = null,
                        AccessFailedCount = 0
                    };
                }

                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var accessFailedCount = await _userManager.GetAccessFailedCountAsync(user);

                return new UserLockoutStatus
                {
                    LockoutEnd = lockoutEnd,
                    AccessFailedCount = accessFailedCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving lockout status for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Removes any active lockout and resets failed access attempts for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> RemoveLockoutAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                // Set lockout end to null (removes lockout)
                var setLockoutResult = await _userManager.SetLockoutEndDateAsync(user, null);
                if (!setLockoutResult.Succeeded)
                {
                    _logger.LogWarning("Failed to remove lockout for user {UserId}: {Errors}",
                        userId, string.Join(", ", setLockoutResult.Errors.Select(e => e.Description)));
                    return false;
                }

                // Reset failed access count
                var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
                if (!resetResult.Succeeded)
                {
                    _logger.LogWarning("Failed to reset access failed count for user {UserId}: {Errors}",
                        userId, string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                    return false;
                }

                _logger.LogInformation("Removed lockout and reset failed access count for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing lockout for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Sets a permanent lockout for a user by setting the lockout end to the maximum date.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> SetPermanentLockoutAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                // Set lockout to maximum date (permanent lockout)
                var lockoutEnd = DateTimeOffset.MaxValue.AddDays(-1);
                var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

                if (result.Succeeded)
                {
                    _logger.LogWarning("Set permanent lockout for user {UserId}", userId);
                    return true;
                }

                _logger.LogWarning("Failed to set permanent lockout for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting permanent lockout for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Sets a temporary lockout for a user starting from now for a specified duration.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="duration">The duration of the lockout from now.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> SetTemporaryLockoutAsync(string userId, TimeSpan duration)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                var lockoutEnd = DateTimeOffset.UtcNow.Add(duration);
                var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

                if (result.Succeeded)
                {
                    _logger.LogWarning("Set temporary lockout for user {UserId} until {LockoutEnd} (duration: {Duration})",
                        userId, lockoutEnd, duration);
                    return true;
                }

                _logger.LogWarning("Failed to set temporary lockout for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting temporary lockout for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Sets a lockout that ends at a specific date and time.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="lockoutEnd">The date and time when the lockout will end.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> SetLockoutEndAsync(string userId, DateTimeOffset lockoutEnd)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                // Ensure lockout end is in the future
                if (lockoutEnd <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("Attempted to set lockout end in the past for user {UserId}", userId);
                    return false;
                }

                var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

                if (result.Succeeded)
                {
                    _logger.LogWarning("Set lockout for user {UserId} until {LockoutEnd}",
                        userId, lockoutEnd);
                    return true;
                }

                _logger.LogWarning("Failed to set lockout end for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting lockout end for user {UserId}", userId);
                throw;
            }
        }
    }
}
