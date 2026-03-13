namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for comment entities (Comment, GroupPostComment)
    /// </summary>
    public interface IPostComment
    {
        /// <summary>
        /// Unique identifier for the comment
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// The post this comment belongs to
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// The user who created the comment
        /// </summary>
        string AuthorId { get; set; }

        /// <summary>
        /// Navigation property for the comment author
        /// </summary>
        ApplicationUser Author { get; set; }

        /// <summary>
        /// The text content of the comment
        /// </summary>
        string Content { get; set; }

        /// <summary>
        /// Optional image attached to the comment
        /// </summary>
        string? ImageUrl { get; set; }

        /// <summary>
        /// When the comment was created
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// Optional parent comment for nested replies (null for top-level comments)
        /// </summary>
        int? ParentCommentId { get; set; }
    }

    /// <summary>
    /// Generic interface for comments with navigation properties
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    /// <typeparam name="TComment">The comment entity type (self-referential for replies)</typeparam>
    public interface IPostComment<TPost, TComment> : IPostComment
        where TPost : class, IPostEntity
        where TComment : class, IPostComment
    {
        /// <summary>
        /// Navigation property for the parent post
        /// </summary>
        TPost Post { get; set; }

        /// <summary>
        /// Navigation property for the parent comment (for replies)
        /// </summary>
        TComment? ParentComment { get; set; }

        /// <summary>
        /// Navigation property for replies to this comment
        /// </summary>
        ICollection<TComment> Replies { get; set; }
    }
}
