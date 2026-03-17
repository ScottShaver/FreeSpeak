namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the approval status of a group post.
    /// Used to control post visibility when groups require moderator approval.
    /// </summary>
    public enum PostStatus
    {
        /// <summary>
        /// The post is pending moderator approval.
        /// Post is not visible to regular group members.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The post has been approved and is visible to all group members.
        /// This is the default status for groups that don't require post approval.
        /// </summary>
        Posted = 1,

        /// <summary>
        /// The post was declined by a moderator and will not be published.
        /// Post remains hidden from all group members except the author.
        /// </summary>
        Declined = 2
    }
}
