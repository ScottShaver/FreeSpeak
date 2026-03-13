using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a like/reaction on a comment in the main feed.
    /// Supports different reaction types (Like, Love, Care, etc.).
    /// Implements ICommentLike for repository pattern abstraction.
    /// </summary>
    public class CommentLike : ICommentLike<Comment>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the like.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the comment that was liked.
        /// </summary>
        public required int CommentId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the liked comment.
        /// </summary>
        public Comment Comment { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who created the like.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who liked the comment.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the type of reaction (Like, Love, Care, Haha, Wow, Sad, Angry).
        /// Defaults to Like.
        /// </summary>
        public LikeType Type { get; set; } = LikeType.Like;

        /// <summary>
        /// Gets or sets the timestamp when the like was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
