using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a post within a group's feed.
    /// Similar to main feed posts but scoped to a specific group.
    /// Implements IGroupPostEntity for repository pattern abstraction.
    /// </summary>
    public class GroupPost : IGroupPostEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the group post.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group this post belongs to.
        /// </summary>
        public required int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who created the post.
        /// </summary>
        public required string AuthorId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the author's user profile.
        /// </summary>
        public ApplicationUser Author { get; set; } = null!;

        /// <summary>
        /// Gets or sets the main text content of the post.
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the post was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when the post was last updated.
        /// Null if the post has never been edited.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the collection of comments on this group post.
        /// </summary>
        public ICollection<GroupPostComment> Comments { get; set; } = new List<GroupPostComment>();

        /// <summary>
        /// Gets or sets the collection of likes/reactions on this group post.
        /// </summary>
        public ICollection<GroupPostLike> Likes { get; set; } = new List<GroupPostLike>();

        /// <summary>
        /// Gets or sets the collection of images attached to this group post.
        /// </summary>
        public ICollection<GroupPostImage> Images { get; set; } = new List<GroupPostImage>();

        /// <summary>
        /// Gets or sets the cached count of likes on this post.
        /// Updated via database trigger or application logic for performance.
        /// </summary>
        public int LikeCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the cached count of comments on this post.
        /// Updated via database trigger or application logic for performance.
        /// </summary>
        public int CommentCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the cached count of times this post has been shared.
        /// Updated when the post is shared by users.
        /// </summary>
        public int ShareCount { get; set; } = 0;

        #region Explicit Interface Implementation
        /// <summary>
        /// Explicit implementation of IGroupPostEntity.GroupId for interface contract.
        /// </summary>
        int IGroupPostEntity.GroupId
        {
            get => GroupId;
            set => GroupId = value;
        }

        /// <summary>
        /// Explicit implementation of IPostEntity.AuthorId for interface contract.
        /// </summary>
        string IPostEntity.AuthorId
        {
            get => AuthorId;
            set => AuthorId = value;
        }

        /// <summary>
        /// Explicit implementation of IPostEntity.Content for interface contract.
        /// </summary>
        string IPostEntity.Content
        {
            get => Content;
            set => Content = value;
        }
        #endregion
    }
}
