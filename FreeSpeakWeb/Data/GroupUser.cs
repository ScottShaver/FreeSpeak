namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user's membership in a group, including their role, activity, and statistics.
    /// Tracks join date, post count, points, administrative permissions, and rule acceptance.
    /// </summary>
    public class GroupUser
    {
        /// <summary>
        /// Gets or sets the unique identifier for this group membership record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who is a member of the group.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the timestamp when the user joined the group.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the number of posts the user has created in this group.
        /// Updated when user creates or deletes posts. Defaults to 0.
        /// </summary>
        public int PostCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the points the user has earned through activity in this group.
        /// Can be used for gamification or reputation systems. Defaults to 0.
        /// </summary>
        public int GroupPoints { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the user has administrator privileges in this group.
        /// Admins can manage group settings, members, and moderate content. Defaults to false.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the user has moderator privileges in this group.
        /// Moderators can moderate content but have fewer permissions than admins. Defaults to false.
        /// </summary>
        public bool IsModerator { get; set; } = false;

        /// <summary>
        /// Gets or sets the timestamp of the user's last activity in this group.
        /// Updated when user posts, comments, or interacts. Defaults to UTC now.
        /// </summary>
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the user has acknowledged and agreed to the group's rules.
        /// May be required before the user can post in the group. Defaults to false.
        /// </summary>
        public bool HasAgreedToRules { get; set; } = false;
    }
}
