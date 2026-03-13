using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    public class Comment : IPostComment
    {
        public int Id { get; set; }

        /// <summary>
        /// The post this comment belongs to
        /// </summary>
        public required int PostId { get; set; }
        public Post Post { get; set; } = null!;

        /// <summary>
        /// The user who created the comment
        /// </summary>
        public required string AuthorId { get; set; }
        public ApplicationUser Author { get; set; } = null!;

        /// <summary>
        /// The text content of the comment
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Optional image attached to the comment
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// When the comment was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional parent comment for nested replies (null for top-level comments)
        /// </summary>
        public int? ParentCommentId { get; set; }
        public Comment? ParentComment { get; set; }

        /// <summary>
        /// Navigation property for replies to this comment
        /// </summary>
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
