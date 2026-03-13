using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a comment on a post in the main feed.
    /// Supports nested replies through the ParentCommentId property.
    /// Implements IPostComment for repository pattern abstraction.
    /// </summary>
    public class Comment : IPostComment
    {
        /// <summary>
        /// Gets or sets the unique identifier for the comment.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this comment belongs to.
        /// </summary>
        public required int PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent post.
        /// </summary>
        public Post Post { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who created the comment.
        /// </summary>
        public required string AuthorId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the author's user profile.
        /// </summary>
        public ApplicationUser Author { get; set; } = null!;

        /// <summary>
        /// Gets or sets the text content of the comment.
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Gets or sets the optional URL to an image attached to the comment.
        /// Null if no image is attached.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the comment was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the ID of the parent comment for nested replies.
        /// Null for top-level comments directly on the post.
        /// </summary>
        public int? ParentCommentId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent comment.
        /// Null for top-level comments.
        /// </summary>
        public Comment? ParentComment { get; set; }

        /// <summary>
        /// Gets or sets the collection of replies to this comment.
        /// Empty for comments with no replies.
        /// </summary>
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
