using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a like/reaction on a group post.
    /// Supports different reaction types (Like, Love, Care, etc.).
    /// Implements IPostLike for repository pattern abstraction.
    /// </summary>
    public class GroupPostLike : IPostLike<GroupPost>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the like.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group post that was liked.
        /// </summary>
        public required int PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the liked group post.
        /// </summary>
        public GroupPost Post { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who created the like.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who liked the post.
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
