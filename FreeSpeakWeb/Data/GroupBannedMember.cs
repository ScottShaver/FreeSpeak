namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a member who has been banned from a group
    /// </summary>
    public class GroupBannedMember
    {
        public int Id { get; set; }

        /// <summary>
        /// The ID of the group the user is banned from
        /// </summary>
        public required int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        /// <summary>
        /// The ID of the user who was banned
        /// </summary>
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// When the user was banned
        /// </summary>
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }
}
