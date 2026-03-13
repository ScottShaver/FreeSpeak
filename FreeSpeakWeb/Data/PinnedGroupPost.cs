namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a group post that a user has pinned for easy access.
    /// Pinned group posts remain prominently displayed in the user's interface.
    /// </summary>
    public class PinnedGroupPost
    {
        /// <summary>
        /// Gets or sets the unique identifier for this pinned group post record.
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
        /// Gets or sets the ID of the group post that was pinned.
        /// </summary>
        public int PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the pinned group post.
        /// </summary>
        public GroupPost Post { get; set; } = null!;

        /// <summary>
        /// Gets or sets the timestamp when the post was pinned.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
    }
}
