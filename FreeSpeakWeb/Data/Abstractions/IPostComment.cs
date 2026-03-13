namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for comment entities (Comment, GroupPostComment).
    /// Provides common properties for all comment implementations, enabling polymorphic operations
    /// and shared repository logic across both feed comments and group post comments.
    /// Supports nested comment replies through ParentCommentId.
    /// </summary>
    public interface IPostComment
    {
        /// <summary>
        /// Gets or sets the unique identifier for the comment.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this comment belongs to.
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who created the comment.
        /// </summary>
        string AuthorId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the comment author.
        /// </summary>
        ApplicationUser Author { get; set; }

        /// <summary>
        /// Gets or sets the text content of the comment.
        /// </summary>
        string Content { get; set; }

        /// <summary>
        /// Gets or sets the optional URL to an image attached to the comment.
        /// Null if no image is attached.
        /// </summary>
        string? ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the comment was created.
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the ID of the parent comment for nested replies.
        /// Null for top-level comments directly on the post.
        /// </summary>
        int? ParentCommentId { get; set; }
    }

    /// <summary>
    /// Generic interface for comments with strongly-typed navigation properties.
    /// Enables type-safe operations while maintaining polymorphic behavior through the base interface.
    /// Supports nested comment hierarchies through self-referential navigation properties.
    /// </summary>
    /// <typeparam name="TPost">The specific post entity type (Post or GroupPost).</typeparam>
    /// <typeparam name="TComment">The comment entity type (self-referential for reply navigation).</typeparam>
    public interface IPostComment<TPost, TComment> : IPostComment
        where TPost : class, IPostEntity
        where TComment : class, IPostComment
    {
        /// <summary>
        /// Gets or sets the navigation property to the parent post.
        /// Provides strongly-typed access to the post being commented on.
        /// </summary>
        TPost Post { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent comment.
        /// Used for nested replies; null for top-level comments.
        /// </summary>
        TComment? ParentComment { get; set; }

        /// <summary>
        /// Gets or sets the collection of replies to this comment.
        /// Enables hierarchical comment threading and nested discussions.
        /// </summary>
        ICollection<TComment> Replies { get; set; }
    }
}
