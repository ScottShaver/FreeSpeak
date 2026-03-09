using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing user preferences
    /// </summary>
    public class UserPreferenceService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<UserPreferenceService> _logger;

        // Default preference values
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

        public UserPreferenceService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<UserPreferenceService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Get a preference value for a user, returning default if not set
        /// </summary>
        public async Task<string> GetPreferenceAsync(string userId, PreferenceType preferenceType)
        {
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
        /// Get all preferences for a user
        /// </summary>
        public async Task<Dictionary<PreferenceType, string>> GetAllPreferencesAsync(string userId)
        {
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
        /// Set a preference value for a user
        /// </summary>
        public async Task<bool> SetPreferenceAsync(string userId, PreferenceType preferenceType, string value)
        {
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
        /// Set multiple preferences at once
        /// </summary>
        public async Task<bool> SetPreferencesAsync(string userId, Dictionary<PreferenceType, string> preferences)
        {
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
        /// Get the default value for a preference type
        /// </summary>
        public static string GetDefaultPreference(PreferenceType preferenceType)
        {
            return DefaultPreferences.TryGetValue(preferenceType, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Get notification expiration days for a specific notification type
        /// </summary>
        public async Task<int> GetNotificationExpirationDaysAsync(string userId, NotificationType notificationType)
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
        /// Get color scheme preference
        /// </summary>
        public async Task<string> GetColorSchemeAsync(string userId)
        {
            return await GetPreferenceAsync(userId, PreferenceType.ColorScheme);
        }

        /// <summary>
        /// Set color scheme preference
        /// </summary>
        public async Task<bool> SetColorSchemeAsync(string userId, string colorScheme)
        {
            return await SetPreferenceAsync(userId, PreferenceType.ColorScheme, colorScheme);
        }

        /// <summary>
        /// Get name display preference
        /// </summary>
        public async Task<NameDisplayType> GetNameDisplayTypeAsync(string userId)
        {
            var value = await GetPreferenceAsync(userId, PreferenceType.NameDisplay);
            
            if (Enum.TryParse<NameDisplayType>(value, out var nameDisplay))
            {
                return nameDisplay;
            }

            return NameDisplayType.FullName;
        }

        /// <summary>
        /// Get default audience type preference
        /// </summary>
        public async Task<AudienceType> GetDefaultAudienceTypeAsync(string userId)
        {
            var value = await GetPreferenceAsync(userId, PreferenceType.DefaultAudienceType);
            
            if (Enum.TryParse<AudienceType>(value, out var audienceType))
            {
                return audienceType;
            }

            return AudienceType.Public;
        }

        /// <summary>
        /// Format a user's display name based on their preference
        /// </summary>
        public async Task<string> FormatUserDisplayNameAsync(string userId, string firstName, string lastName, string username)
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
