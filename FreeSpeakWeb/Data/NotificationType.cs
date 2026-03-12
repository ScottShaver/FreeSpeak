namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the type of notification
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// General system notification
        /// </summary>
        System = 0,

        /// <summary>
        /// Friend request received
        /// </summary>
        FriendRequest = 1,

        /// <summary>
        /// Friend request accepted
        /// </summary>
        FriendAccepted = 2,

        /// <summary>
        /// Someone liked your post
        /// </summary>
        PostLiked = 3,

        /// <summary>
        /// Someone commented on your post
        /// </summary>
        PostComment = 4,

        /// <summary>
        /// Someone replied to your comment
        /// </summary>
        CommentReply = 5,

        /// <summary>
        /// Someone liked your comment
        /// </summary>
        CommentLiked = 6,

        /// <summary>
        /// You were mentioned in a post or comment
        /// </summary>
        Mention = 7,

        /// <summary>
        /// Someone liked your group post
        /// </summary>
        GroupPostLiked = 8,

        /// <summary>
        /// Someone commented on your group post
        /// </summary>
        GroupPostComment = 9,

        /// <summary>
        /// Someone replied to your comment in a group
        /// </summary>
        GroupCommentReply = 10,

        /// <summary>
        /// Someone liked your comment in a group
        /// </summary>
        GroupCommentLiked = 11
    }
}
