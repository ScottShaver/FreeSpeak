namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a feed post that a user has pinned to the top of their profile or feed.
    /// Pinned posts remain easily accessible and prominently displayed.
    /// </summary>
    public class PinnedPost
    {
        /// <summary>
        /// Gets or sets the unique identifier for this pinned post record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who pinned the post.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who pinned the post.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the post that was pinned.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the pinned post.
        /// </summary>
        public Post Post { get; set; } = null!;

        /// <summary>
        /// Gets or sets the timestamp when the post was pinned.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
    }
}
