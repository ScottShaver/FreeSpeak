namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the types of user preferences
    /// </summary>
    public enum PreferenceType
    {
        /// <summary>
        /// Color scheme/theme preference (default, dark, light, ocean, forest, sunset, purple, high-contrast)
        /// </summary>
        ColorScheme = 0,

        /// <summary>
        /// How to display user names (FullName, FirstName, Username, FirstNameLastInitial)
        /// </summary>
        NameDisplay = 1,

        /// <summary>
        /// Default audience type for posts (Public, FriendsOnly, OnlyMe)
        /// </summary>
        DefaultAudienceType = 2,

        /// <summary>
        /// Notification expiration for PostLiked notifications (in days, default 15)
        /// </summary>
        NotificationExpiration_PostLiked = 100,

        /// <summary>
        /// Notification expiration for PostComment notifications (in days, default 15)
        /// </summary>
        NotificationExpiration_PostComment = 101,

        /// <summary>
        /// Notification expiration for CommentReply notifications (in days, default 15)
        /// </summary>
        NotificationExpiration_CommentReply = 102,

        /// <summary>
        /// Notification expiration for CommentLiked notifications (in days, default 15)
        /// </summary>
        NotificationExpiration_CommentLiked = 103,

        /// <summary>
        /// Notification expiration for FriendRequest notifications (in days, default 30)
        /// </summary>
        NotificationExpiration_FriendRequest = 104,

        /// <summary>
        /// Notification expiration for FriendAccepted notifications (in days, default 15)
        /// </summary>
        NotificationExpiration_FriendAccepted = 105,

        /// <summary>
        /// Notification expiration for Mention notifications (in days, default 30)
        /// </summary>
        NotificationExpiration_Mention = 106,

        /// <summary>
        /// Notification expiration for System notifications (in days, default 30)
        /// </summary>
        NotificationExpiration_System = 107
    }

    /// <summary>
    /// Enum for how to display user names
    /// </summary>
    public enum NameDisplayType
    {
        /// <summary>
        /// Display full name (e.g., "John Smith")
        /// </summary>
        FullName = 0,

        /// <summary>
        /// Display first name only (e.g., "John")
        /// </summary>
        FirstName = 1,

        /// <summary>
        /// Display username (e.g., "johnsmith")
        /// </summary>
        Username = 2,

        /// <summary>
        /// Display first name and last initial (e.g., "John S.")
        /// </summary>
        FirstNameLastInitial = 3
    }
}
