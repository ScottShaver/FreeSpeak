namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines who can see a post
    /// </summary>
    public enum AudienceType
    {
        /// <summary>
        /// Everyone can see the post
        /// </summary>
        Public = 0,

        /// <summary>
        /// Only friends can see the post
        /// </summary>
        FriendsOnly = 1,

        /// <summary>
        /// Only the author can see the post
        /// </summary>
        MeOnly = 2
    }
}
