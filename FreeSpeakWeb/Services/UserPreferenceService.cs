using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing user preferences including color scheme, name display format,
    /// default audience type, and notification expiration settings.
    /// </summary>
    public class UserPreferenceService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<UserPreferenceService> _logger;

        /// <summary>
        /// Default values for all preference types used when a user has not set a preference.
        /// </summary>
        private static readonly Dictionary<PreferenceType, string> DefaultPreferences = new()
        {
            { PreferenceType.ColorScheme, "default" },
            { PreferenceType.NameDisplay, NameDisplayType.FullName.ToString() },
            { PreferenceType.DefaultAudienceType, AudienceType.FriendsOnly.ToString() },
            { PreferenceType.NotificationExpiration_PostLiked, "15" },
            { PreferenceType.NotificationExpiration_PostComment, "15" },
            { PreferenceType.NotificationExpiration_CommentReply, "15" },
            { PreferenceType.NotificationExpiration_CommentLiked, "15" },
            { PreferenceType.NotificationExpiration_FriendRequest, "30" },
            { PreferenceType.NotificationExpiration_FriendAccepted, "15" },
            { PreferenceType.NotificationExpiration_Mention, "30" },
            { PreferenceType.NotificationExpiration_System, "30" }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="UserPreferenceService"/> class.
        /// </summary>
        /// <param name="contextFactory">The factory for creating database contexts.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        public UserPreferenceService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<UserPreferenceService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets a preference value for a user, returning the default value if not set.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for default values.</param>
        /// <param name="preferenceType">The type of preference to retrieve.</param>
        /// <returns>The user's preference value, or the default value if not set or on error.</returns>
        public async Task<string> GetPreferenceAsync(string? userId, PreferenceType preferenceType)
        {
            // Return default if userId is null
            if (string.IsNullOrEmpty(userId))
            {
                return GetDefaultPreference(preferenceType);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var preference = await context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceType == preferenceType);

                if (preference != null)
                {
                    return preference.PreferenceValue;
                }

                // Return default if not set
                return GetDefaultPreference(preferenceType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preference {PreferenceType} for user {UserId}", preferenceType, userId);
                return GetDefaultPreference(preferenceType);
            }
        }

        /// <summary>
        /// Gets all preferences for a user, filling in defaults for any unset preferences.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for all default values.</param>
        /// <returns>A dictionary mapping preference types to their values.</returns>
        public async Task<Dictionary<PreferenceType, string>> GetAllPreferencesAsync(string? userId)
        {
            // Return all defaults if userId is null
            if (string.IsNullOrEmpty(userId))
            {
                return new Dictionary<PreferenceType, string>(DefaultPreferences);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var preferences = await context.UserPreferences
                    .Where(p => p.UserId == userId)
                    .ToDictionaryAsync(p => p.PreferenceType, p => p.PreferenceValue);

                // Fill in defaults for missing preferences
                var result = new Dictionary<PreferenceType, string>();
                foreach (var defaultPref in DefaultPreferences)
                {
                    result[defaultPref.Key] = preferences.ContainsKey(defaultPref.Key)
                        ? preferences[defaultPref.Key]
                        : defaultPref.Value;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all preferences for user {UserId}", userId);
                return new Dictionary<PreferenceType, string>(DefaultPreferences);
            }
        }

        /// <summary>
        /// Sets a preference value for a user, creating or updating the record as needed.
        /// </summary>
        /// <param name="userId">The unique identifier of the user. Required for setting preferences.</param>
        /// <param name="preferenceType">The type of preference to set.</param>
        /// <param name="value">The preference value to store.</param>
        /// <returns>True if the preference was set successfully, false otherwise.</returns>
        public async Task<bool> SetPreferenceAsync(string? userId, PreferenceType preferenceType, string value)
        {
            // Cannot set preference without valid userId
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot set preference {PreferenceType} - userId is null or empty", preferenceType);
                return false;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var preference = await context.UserPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceType == preferenceType);

                if (preference != null)
                {
                    // Update existing preference
                    preference.PreferenceValue = value;
                    preference.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new preference
                    preference = new UserPreference
                    {
                        UserId = userId,
                        PreferenceType = preferenceType,
                        PreferenceValue = value,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.UserPreferences.Add(preference);
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Set preference {PreferenceType} to {Value} for user {UserId}", 
                    preferenceType, value, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting preference {PreferenceType} for user {UserId}", preferenceType, userId);
                return false;
            }
        }

        /// <summary>
        /// Sets multiple preferences for a user in a single database transaction.
        /// </summary>
        /// <param name="userId">The unique identifier of the user. Required for setting preferences.</param>
        /// <param name="preferences">A dictionary of preference types and values to set.</param>
        /// <returns>True if all preferences were set successfully, false otherwise.</returns>
        public async Task<bool> SetPreferencesAsync(string? userId, Dictionary<PreferenceType, string> preferences)
        {
            // Cannot set preferences without valid userId
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot set preferences - userId is null or empty");
                return false;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                foreach (var pref in preferences)
                {
                    var existing = await context.UserPreferences
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceType == pref.Key);

                    if (existing != null)
                    {
                        existing.PreferenceValue = pref.Value;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        context.UserPreferences.Add(new UserPreference
                        {
                            UserId = userId,
                            PreferenceType = pref.Key,
                            PreferenceValue = pref.Value,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Set {Count} preferences for user {UserId}", preferences.Count, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting multiple preferences for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Gets the default value for a preference type.
        /// </summary>
        /// <param name="preferenceType">The type of preference to get the default for.</param>
        /// <returns>The default preference value, or an empty string if no default is defined.</returns>
        public static string GetDefaultPreference(PreferenceType preferenceType)
        {
            return DefaultPreferences.TryGetValue(preferenceType, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Gets the notification expiration days setting for a specific notification type.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for default values.</param>
        /// <param name="notificationType">The type of notification to get expiration days for.</param>
        /// <returns>The number of days after which notifications of this type should expire.</returns>
        public async Task<int> GetNotificationExpirationDaysAsync(string? userId, NotificationType notificationType)
        {
            var preferenceType = notificationType switch
            {
                NotificationType.PostLiked => PreferenceType.NotificationExpiration_PostLiked,
                NotificationType.PostComment => PreferenceType.NotificationExpiration_PostComment,
                NotificationType.CommentReply => PreferenceType.NotificationExpiration_CommentReply,
                NotificationType.CommentLiked => PreferenceType.NotificationExpiration_CommentLiked,
                NotificationType.FriendRequest => PreferenceType.NotificationExpiration_FriendRequest,
                NotificationType.FriendAccepted => PreferenceType.NotificationExpiration_FriendAccepted,
                NotificationType.Mention => PreferenceType.NotificationExpiration_Mention,
                NotificationType.System => PreferenceType.NotificationExpiration_System,
                _ => PreferenceType.NotificationExpiration_System
            };

            var value = await GetPreferenceAsync(userId, preferenceType);
            
            if (int.TryParse(value, out var days))
            {
                return days;
            }

            // Return default
            var defaultValue = GetDefaultPreference(preferenceType);
            return int.TryParse(defaultValue, out var defaultDays) ? defaultDays : 15;
        }

        /// <summary>
        /// Gets the color scheme preference for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for the default color scheme.</param>
        /// <returns>The user's color scheme preference (e.g., "default", "dark", "light").</returns>
        public async Task<string> GetColorSchemeAsync(string? userId)
        {
            return await GetPreferenceAsync(userId, PreferenceType.ColorScheme);
        }

        /// <summary>
        /// Sets the color scheme preference for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="colorScheme">The color scheme to set (e.g., "default", "dark", "light").</param>
        /// <returns>True if the preference was set successfully, false otherwise.</returns>
        public async Task<bool> SetColorSchemeAsync(string? userId, string colorScheme)
        {
            return await SetPreferenceAsync(userId, PreferenceType.ColorScheme, colorScheme);
        }

        /// <summary>
        /// Gets the name display preference for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for the default display type.</param>
        /// <returns>The user's preferred name display format.</returns>
        public async Task<NameDisplayType> GetNameDisplayTypeAsync(string? userId)
        {
            var value = await GetPreferenceAsync(userId, PreferenceType.NameDisplay);
            
            if (Enum.TryParse<NameDisplayType>(value, out var nameDisplay))
            {
                return nameDisplay;
            }

            return NameDisplayType.FullName;
        }

        /// <summary>
        /// Gets the default audience type preference for a user's posts.
        /// </summary>
        /// <param name="userId">The unique identifier of the user, or null for the default audience type.</param>
        /// <returns>The user's preferred default audience type for new posts.</returns>
        public async Task<AudienceType> GetDefaultAudienceTypeAsync(string? userId)
        {
            var value = await GetPreferenceAsync(userId, PreferenceType.DefaultAudienceType);

            if (Enum.TryParse<AudienceType>(value, out var audienceType))
            {
                return audienceType;
            }

            return AudienceType.FriendsOnly;
        }

        /// <summary>
        /// Formats a user's display name based on their name display preference.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose preference to use.</param>
        /// <param name="firstName">The user's first name.</param>
        /// <param name="lastName">The user's last name.</param>
        /// <param name="username">The user's username.</param>
        /// <returns>The formatted display name according to the user's preference.</returns>
        public async Task<string> FormatUserDisplayNameAsync(string? userId, string firstName, string lastName, string username)
        {
            var nameDisplay = await GetNameDisplayTypeAsync(userId);

            return nameDisplay switch
            {
                NameDisplayType.FullName => $"{firstName} {lastName}".Trim(),
                NameDisplayType.FirstName => firstName,
                NameDisplayType.Username => username,
                NameDisplayType.FirstNameLastInitial => $"{firstName} {(!string.IsNullOrEmpty(lastName) ? lastName[0] + "." : "")}".Trim(),
                _ => $"{firstName} {lastName}".Trim()
            };
        }
    }
}
