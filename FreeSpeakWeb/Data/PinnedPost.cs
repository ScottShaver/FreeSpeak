namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a post that a user has pinned
    /// </summary>
    public class PinnedPost
    {
        public int Id { get; set; }

        /// <summary>
        /// The ID of the user who pinned the post
        /// </summary>
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// The ID of the post that was pinned
        /// </summary>
        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

        /// <summary>
        /// When the post was pinned
        /// </summary>
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
    }
}
