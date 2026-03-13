namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user who has been banned from a group.
    /// Banned members cannot view or interact with the group's content.
    /// </summary>
    public class GroupBannedMember
    {
        /// <summary>
        /// Gets or sets the unique identifier for this ban record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group the user is banned from.
        /// </summary>
        public required int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who was banned.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the banned user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the timestamp when the user was banned from the group.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }
}
