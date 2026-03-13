namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for post like entities (Like, GroupPostLike).
    /// Provides common properties for all post like implementations, enabling polymorphic operations
    /// across both feed posts and group posts in the repository layer.
    /// </summary>
    public interface IPostLike
    {
        /// <summary>
        /// Gets or sets the unique identifier for the like.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this like belongs to.
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who created the like.
        /// </summary>
        string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who created the like.
        /// </summary>
        ApplicationUser User { get; set; }

        /// <summary>
        /// Gets or sets the type of reaction (Like, Love, Care, etc.).
        /// </summary>
        LikeType Type { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the like was created.
        /// </summary>
        DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Generic interface for post likes with strongly-typed navigation to the parent post.
    /// Enables type-safe operations while maintaining polymorphic behavior through the base interface.
    /// </summary>
    /// <typeparam name="TPost">The specific post entity type (Post or GroupPost).</typeparam>
    public interface IPostLike<TPost> : IPostLike
        where TPost : class, IPostEntity
    {
        /// <summary>
        /// Gets or sets the navigation property to the parent post.
        /// Provides strongly-typed access to the liked post.
        /// </summary>
        TPost Post { get; set; }
    }

    /// <summary>
    /// Base interface for comment like entities (CommentLike, GroupPostCommentLike).
    /// Provides common properties for all comment like implementations, enabling polymorphic operations
    /// across both feed comments and group post comments in the repository layer.
    /// </summary>
    public interface ICommentLike
    {
        /// <summary>
        /// Gets or sets the unique identifier for the like.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the comment this like belongs to.
        /// </summary>
        int CommentId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who created the like.
        /// </summary>
        string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who created the like.
        /// </summary>
        ApplicationUser User { get; set; }

        /// <summary>
        /// Gets or sets the type of reaction (Like, Love, Care, etc.).
        /// </summary>
        LikeType Type { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the like was created.
        /// </summary>
        DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Generic interface for comment likes with strongly-typed navigation to the parent comment.
    /// Enables type-safe operations while maintaining polymorphic behavior through the base interface.
    /// </summary>
    /// <typeparam name="TComment">The specific comment entity type (Comment or GroupPostComment).</typeparam>
    public interface ICommentLike<TComment> : ICommentLike
        where TComment : class, IPostComment
    {
        /// <summary>
        /// Gets or sets the navigation property to the parent comment.
        /// Provides strongly-typed access to the liked comment.
        /// </summary>
        TComment Comment { get; set; }
    }
}
