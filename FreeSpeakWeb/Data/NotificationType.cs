namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the various types of notifications that can be sent to users.
    /// Used to categorize notifications and determine appropriate routing and display behavior.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// General system-generated notification.
        /// Used for administrative messages, announcements, or system updates.
        /// </summary>
        System = 0,

        /// <summary>
        /// Notification that a user has received a friend request.
        /// Recipient can accept, reject, or ignore the request.
        /// </summary>
        FriendRequest = 1,

        /// <summary>
        /// Notification that a friend request has been accepted.
        /// Sent to the original requester when their friend request is approved.
        /// </summary>
        FriendAccepted = 2,

        /// <summary>
        /// Notification that someone has liked a user's feed post.
        /// </summary>
        PostLiked = 3,

        /// <summary>
        /// Notification that someone has commented on a user's feed post.
        /// </summary>
        PostComment = 4,

        /// <summary>
        /// Notification that someone has replied to a user's comment on a feed post.
        /// </summary>
        CommentReply = 5,

        /// <summary>
        /// Notification that someone has liked a user's comment on a feed post.
        /// </summary>
        CommentLiked = 6,

        /// <summary>
        /// Notification that a user was mentioned in a post or comment.
        /// Typically triggered by @username syntax.
        /// </summary>
        Mention = 7,

        /// <summary>
        /// Notification that someone has liked a user's group post.
        /// </summary>
        GroupPostLiked = 8,

        /// <summary>
        /// Notification that someone has commented on a user's group post.
        /// </summary>
        GroupPostComment = 9,

        /// <summary>
        /// Notification that someone has replied to a user's comment in a group.
        /// </summary>
        GroupCommentReply = 10,

        /// <summary>
        /// Notification that someone has liked a user's comment in a group.
        /// </summary>
        GroupCommentLiked = 11,

        /// <summary>
        /// Notification that a user's request to join a group has been approved.
        /// Sent to the user who submitted the join request.
        /// </summary>
        GroupJoinApproved = 12
    }
}
