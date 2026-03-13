namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for all post entities (Post, GroupPost).
    /// Defines the common structure shared across all post types, enabling polymorphic operations
    /// and shared repository logic for both feed posts and group posts.
    /// </summary>
    public interface IPostEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the post.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who created the post.
        /// </summary>
        string AuthorId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the post author.
        /// </summary>
        ApplicationUser Author { get; set; }

        /// <summary>
        /// Gets or sets the main text content of the post.
        /// </summary>
        string Content { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the post was created.
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the post was last updated.
        /// Null if the post has never been edited.
        /// </summary>
        DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the cached count of likes on the post.
        /// Updated via database trigger or application logic for performance.
        /// </summary>
        int LikeCount { get; set; }

        /// <summary>
        /// Gets or sets the cached count of comments on the post.
        /// Updated via database trigger or application logic for performance.
        /// </summary>
        int CommentCount { get; set; }

        /// <summary>
        /// Gets or sets the cached count of shares for the post.
        /// Incremented when users share or repost the content.
        /// </summary>
        int ShareCount { get; set; }
    }

    /// <summary>
    /// Marker interface for post entities that belong to a specific group.
    /// Extends IPostEntity with group-specific properties, enabling group-scoped operations
    /// while maintaining compatibility with generic post operations.
    /// </summary>
    public interface IGroupPostEntity : IPostEntity
    {
        /// <summary>
        /// Gets or sets the ID of the group this post belongs to.
        /// </summary>
        int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent group.
        /// </summary>
        Group Group { get; set; }
    }

    /// <summary>
    /// Marker interface for feed post entities with audience visibility controls.
    /// Extends IPostEntity with privacy settings, allowing users to control
    /// who can view their posts (Public, FriendsOnly, MeOnly).
    /// </summary>
    public interface IFeedPostEntity : IPostEntity
    {
        /// <summary>
        /// Gets or sets the audience visibility level for this post.
        /// Determines who can view the post content.
        /// </summary>
        AudienceType AudienceType { get; set; }
    }
}
