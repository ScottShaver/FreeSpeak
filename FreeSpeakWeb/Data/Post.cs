namespace FreeSpeakWeb.Data
{
    public class Post
    {
        public int Id { get; set; }

        /// <summary>
        /// The user who created the post
        /// </summary>
        public required string AuthorId { get; set; }
        public ApplicationUser Author { get; set; } = null!;

        /// <summary>
        /// The main text content of the post
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// When the post was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the post was last updated (null if never updated)
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Navigation property for comments on this post
        /// </summary>
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();

        /// <summary>
        /// Navigation property for likes on this post
        /// </summary>
        public ICollection<Like> Likes { get; set; } = new List<Like>();

        /// <summary>
        /// Navigation property for images attached to this post
        /// </summary>
        public ICollection<PostImage> Images { get; set; } = new List<PostImage>();

        /// <summary>
        /// Cached count of likes (updated via trigger or application logic)
        /// </summary>
        public int LikeCount { get; set; } = 0;

        /// <summary>
        /// Cached count of comments (updated via trigger or application logic)
        /// <summary>
        /// Cached count of comments (updated via trigger or application logic)
        /// </summary>
        public int CommentCount { get; set; } = 0;

        /// <summary>
        /// Cached count of shares (updated when post is shared)
        /// </summary>
        public int ShareCount { get; set; } = 0;

        /// <summary>
        /// Defines who can see this post
        /// </summary>
        public AudienceType AudienceType { get; set; } = AudienceType.Public;
    }
}
