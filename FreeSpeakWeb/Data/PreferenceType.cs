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
        NotificationExpiration_System = 107
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
