using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    public class GroupPost : IGroupPostEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// The group this post belongs to
        /// </summary>
        public required int GroupId { get; set; }
        public Group Group { get; set; } = null!;

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
        public ICollection<GroupPostComment> Comments { get; set; } = new List<GroupPostComment>();

        /// <summary>
        /// Navigation property for likes on this post
        /// </summary>
        public ICollection<GroupPostLike> Likes { get; set; } = new List<GroupPostLike>();

        /// <summary>
        /// Navigation property for images attached to this post
        /// </summary>
        public ICollection<GroupPostImage> Images { get; set; } = new List<GroupPostImage>();

        /// <summary>
        /// Cached count of likes (updated via trigger or application logic)
        /// </summary>
        public int LikeCount { get; set; } = 0;

        /// <summary>
        /// Cached count of comments (updated via trigger or application logic)
        /// </summary>
        public int CommentCount { get; set; } = 0;

        /// <summary>
        /// Cached count of shares (updated when post is shared)
        /// </summary>
        public int ShareCount { get; set; } = 0;

        #region Explicit Interface Implementation
        int IGroupPostEntity.GroupId
        {
            get => GroupId;
            set => GroupId = value;
        }

        string IPostEntity.AuthorId
        {
            get => AuthorId;
            set => AuthorId = value;
        }

        string IPostEntity.Content
        {
            get => Content;
            set => Content = value;
        }
        #endregion
    }
}
