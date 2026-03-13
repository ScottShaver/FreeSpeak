namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for all post entities (Post, GroupPost)
    /// Defines the common structure shared across post types
    /// </summary>
    public interface IPostEntity
    {
        /// <summary>
        /// Unique identifier for the post
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// The user who created the post
        /// </summary>
        string AuthorId { get; set; }

        /// <summary>
        /// Navigation property for the post author
        /// </summary>
        ApplicationUser Author { get; set; }

        /// <summary>
        /// The main text content of the post
        /// </summary>
        string Content { get; set; }

        /// <summary>
        /// When the post was created
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the post was last updated (null if never updated)
        /// </summary>
        DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Cached count of likes
        /// </summary>
        int LikeCount { get; set; }

        /// <summary>
        /// Cached count of comments
        /// </summary>
        int CommentCount { get; set; }

        /// <summary>
        /// Cached count of shares
        /// </summary>
        int ShareCount { get; set; }
    }

    /// <summary>
    /// Marker interface for post entities that belong to a group
    /// </summary>
    public interface IGroupPostEntity : IPostEntity
    {
        /// <summary>
        /// The group this post belongs to
        /// </summary>
        int GroupId { get; set; }

        /// <summary>
        /// Navigation property for the group
        /// </summary>
        Group Group { get; set; }
    }

    /// <summary>
    /// Marker interface for feed post entities with audience control
    /// </summary>
    public interface IFeedPostEntity : IPostEntity
    {
        /// <summary>
        /// Defines who can see this post
        /// </summary>
        AudienceType AudienceType { get; set; }
    }
}
