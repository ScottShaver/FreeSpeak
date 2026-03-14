namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Service interface for managing user account lockouts in ASP.NET Core Identity.
    /// Provides methods for checking lockout status, setting temporary or permanent lockouts,
    /// and removing lockouts.
    /// </summary>
    public interface IUserLockoutService
    {
        /// <summary>
        /// Gets the current lockout status for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A lockout status object containing lockout end time and failed access count.</returns>
        Task<UserLockoutStatus> GetLockoutStatusAsync(string userId);

        /// <summary>
        /// Removes any active lockout and resets failed access attempts for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> RemoveLockoutAsync(string userId);

        /// <summary>
        /// Sets a permanent lockout for a user by setting the lockout end to the maximum date.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> SetPermanentLockoutAsync(string userId);

        /// <summary>
        /// Sets a temporary lockout for a user starting from now for a specified duration.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="duration">The duration of the lockout from now.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> SetTemporaryLockoutAsync(string userId, TimeSpan duration);

        /// <summary>
        /// Sets a lockout that ends at a specific date and time.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="lockoutEnd">The date and time when the lockout will end.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> SetLockoutEndAsync(string userId, DateTimeOffset lockoutEnd);
    }

    /// <summary>
    /// Represents the lockout status of a user account.
    /// </summary>
    public class UserLockoutStatus
    {
        /// <summary>
        /// Gets or sets the date and time when the lockout ends.
        /// Null if the user is not locked out.
        /// </summary>
        public DateTimeOffset? LockoutEnd { get; set; }

        /// <summary>
        /// Gets or sets the number of failed access attempts.
        /// </summary>
        public int AccessFailedCount { get; set; }

        /// <summary>
        /// Gets a value indicating whether the user is permanently locked out.
        /// A permanent lockout is indicated by a lockout end date more than 100 years in the future.
        /// </summary>
        public bool IsPermanentlyLocked => LockoutEnd.HasValue && 
            LockoutEnd.Value > DateTimeOffset.UtcNow.AddYears(100);

        /// <summary>
        /// Gets a value indicating whether the user is currently locked out.
        /// </summary>
        public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
    }
}
