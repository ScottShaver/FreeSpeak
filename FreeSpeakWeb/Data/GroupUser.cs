namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user's membership in a group
    /// </summary>
    public class GroupUser
    {
        /// <summary>
        /// Unique identifier for the group membership
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the group
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Navigation property to the group
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// The ID of the user who is a member
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Navigation property to the user
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// When the user joined the group
        /// </summary>
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of posts the user has made in this group
        /// </summary>
        public int PostCount { get; set; } = 0;

        /// <summary>
        /// Points the user has earned in this group
        /// </summary>
        public int GroupPoints { get; set; } = 0;

        /// <summary>
        /// Whether the user is an administrator of this group
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is a moderator of this group
        /// </summary>
        public bool IsModerator { get; set; } = false;

        /// <summary>
        /// When the user was last active in this group
        /// </summary>
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the user has agreed to the group rules
        /// </summary>
        public bool HasAgreedToRules { get; set; } = false;
    }
}
