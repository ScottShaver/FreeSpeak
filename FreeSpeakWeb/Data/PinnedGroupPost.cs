namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a group post that a user has pinned
    /// </summary>
    public class PinnedGroupPost
    {
        public int Id { get; set; }

        /// <summary>
        /// The ID of the user who pinned the post
        /// </summary>
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// The ID of the group post that was pinned
        /// </summary>
        public int PostId { get; set; }
        public GroupPost Post { get; set; } = null!;

        /// <summary>
        /// When the post was pinned
        /// </summary>
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
    }
}
