namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the various types of user preferences that can be customized.
    /// Includes UI preferences, default behaviors, and notification expiration settings.
    /// </summary>
    public enum PreferenceType
    {
        /// <summary>
        /// User's preferred color scheme or theme.
        /// Valid values: default, dark, light, ocean, forest, sunset, purple, high-contrast.
        /// </summary>
        ColorScheme = 0,

        /// <summary>
        /// How user names should be displayed throughout the application.
        /// Valid values: FullName, FirstName, Username, FirstNameLastInitial.
        /// </summary>
        NameDisplay = 1,

        /// <summary>
        /// Default audience/visibility type when creating new posts.
        /// Valid values: Public, FriendsOnly, OnlyMe.
        /// </summary>
        DefaultAudienceType = 2,

        /// <summary>
        /// Number of days before PostLiked notifications expire and are auto-deleted.
        /// Default: 15 days.
        /// </summary>
        NotificationExpiration_PostLiked = 100,

        /// <summary>
        /// Number of days before PostComment notifications expire and are auto-deleted.
        /// Default: 15 days.
        /// </summary>
        NotificationExpiration_PostComment = 101,

        /// <summary>
        /// Number of days before CommentReply notifications expire and are auto-deleted.
        /// Default: 15 days.
        /// </summary>
        NotificationExpiration_CommentReply = 102,

        /// <summary>
        /// Number of days before CommentLiked notifications expire and are auto-deleted.
        /// Default: 15 days.
        /// </summary>
        NotificationExpiration_CommentLiked = 103,

        /// <summary>
        /// Number of days before FriendRequest notifications expire and are auto-deleted.
        /// Default: 30 days.
        /// </summary>
        NotificationExpiration_FriendRequest = 104,

        /// <summary>
        /// Number of days before FriendAccepted notifications expire and are auto-deleted.
        /// Default: 15 days.
        /// </summary>
        NotificationExpiration_FriendAccepted = 105,

        /// <summary>
        /// Number of days before Mention notifications expire and are auto-deleted.
        /// Default: 30 days.
        /// </summary>
        NotificationExpiration_Mention = 106,

        /// <summary>
        /// Number of days before System notifications expire and are auto-deleted.
        /// Default: 30 days.
        /// </summary>
        NotificationExpiration_System = 107,

        /// <summary>
        /// User's preferred timezone for displaying dates and times.
        /// Valid values: IANA timezone identifiers (e.g., "America/New_York", "Europe/London", "UTC").
        /// Default: "America/New_York" (US Eastern Time).
        /// </summary>
        Timezone = 3,

        /// <summary>
        /// User's country or region for non-localization purposes (e.g., regional content).
        /// Valid values: ISO 3166-1 alpha-2 country codes (e.g., "US", "GB", "CA").
        /// Default: "US" (United States of America).
        /// Note: For UI localization, use Culture instead.
        /// </summary>
        Country = 4,

        /// <summary>
        /// User's preferred language for the user interface.
        /// Valid values: ISO 639-1 language codes (e.g., "en", "es", "fr", "de").
        /// Default: "en" (English).
        /// Note: This is deprecated. Use Culture instead for localization.
        /// </summary>
        [Obsolete("Use Culture instead for UI localization.")]
        Language = 5,

        /// <summary>
        /// User's preferred culture for UI localization (language and regional formatting).
        /// Valid values: .NET culture identifiers (e.g., "en-US", "fr-FR", "es-ES", "de-DE").
        /// Default: "en-US" (English - United States).
        /// This determines both the UI language and regional formatting (dates, numbers, etc.).
        /// </summary>
        Culture = 6
    }

    /// <summary>
    /// Defines how user names should be displayed throughout the application.
    /// Allows users to customize their privacy level and interface preference.
    /// </summary>
    public enum NameDisplayType
    {
        /// <summary>
        /// Display the user's full name (e.g., "John Smith").
        /// Most formal and complete display option.
        /// </summary>
        FullName = 0,

        /// <summary>
        /// Display only the user's first name (e.g., "John").
        /// More casual and friendly display option.
        /// </summary>
        FirstName = 1,

        /// <summary>
        /// Display the user's username (e.g., "johnsmith").
        /// Provides privacy by not showing real name.
        /// </summary>
        Username = 2,

        /// <summary>
        /// Display first name with last initial (e.g., "John S.").
        /// Balances friendliness with some privacy protection.
        /// </summary>
        FirstNameLastInitial = 3
    }
}
