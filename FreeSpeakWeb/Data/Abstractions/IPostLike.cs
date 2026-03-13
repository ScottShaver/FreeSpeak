namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for post like entities (Like, GroupPostLike)
    /// </summary>
    public interface IPostLike
    {
        /// <summary>
        /// Unique identifier for the like
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// The post this like belongs to
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// The user who created the like
        /// </summary>
        string UserId { get; set; }

        /// <summary>
        /// Navigation property for the user
        /// </summary>
        ApplicationUser User { get; set; }

        /// <summary>
        /// The type of like reaction
        /// </summary>
        LikeType Type { get; set; }

        /// <summary>
        /// When the like was created
        /// </summary>
        DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Generic interface for post likes with navigation to the post
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    public interface IPostLike<TPost> : IPostLike
        where TPost : class, IPostEntity
    {
        /// <summary>
        /// Navigation property for the parent post
        /// </summary>
        TPost Post { get; set; }
    }

    /// <summary>
    /// Base interface for comment like entities (CommentLike, GroupPostCommentLike)
    /// </summary>
    public interface ICommentLike
    {
        /// <summary>
        /// Unique identifier for the like
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// The comment this like belongs to
        /// </summary>
        int CommentId { get; set; }

        /// <summary>
        /// The user who created the like
        /// </summary>
        string UserId { get; set; }

        /// <summary>
        /// Navigation property for the user
        /// </summary>
        ApplicationUser User { get; set; }

        /// <summary>
        /// The type of like reaction
        /// </summary>
        LikeType Type { get; set; }

        /// <summary>
        /// When the like was created
        /// </summary>
        DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Generic interface for comment likes with navigation to the comment
    /// </summary>
    /// <typeparam name="TComment">The comment entity type</typeparam>
    public interface ICommentLike<TComment> : ICommentLike
        where TComment : class, IPostComment
    {
        /// <summary>
        /// Navigation property for the parent comment
        /// </summary>
        TComment Comment { get; set; }
    }
}
