namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the visibility level for posts, controlling who can view the content.
    /// Provides privacy controls similar to common social media platforms.
    /// </summary>
    public enum AudienceType
    {
        /// <summary>
        /// Post is visible to everyone, including non-friends and public visitors.
        /// Default visibility level for maximum reach.
        /// </summary>
        Public = 0,

        /// <summary>
        /// Post is visible only to users who are confirmed friends with the author.
        /// Provides a middle level of privacy for sharing with trusted connections.
        /// </summary>
        FriendsOnly = 1,

        /// <summary>
        /// Post is visible only to the author themselves.
        /// Useful for private notes, drafts, or personal content.
        /// </summary>
        MeOnly = 2
    }
}
