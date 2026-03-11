namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a group where users can join to post and read posts about a specific topic
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Unique identifier for the group
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The name of the group
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Description of what the group is about
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// When the group was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this is a public group (visible to all users)
        /// </summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// Whether this group is hidden from search results and discovery
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// When the group was last active (last post or activity)
        /// </summary>
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of members in the group
        /// </summary>
        public int MemberCount { get; set; } = 0;

        /// <summary>
        /// Whether new members need approval to join
        /// </summary>
        public bool RequiresJoinApproval { get; set; } = false;

        /// <summary>
        /// The ID of the user who created this group
        /// </summary>
        public required string CreatorId { get; set; }

        /// <summary>
        /// Navigation property to the creator user
        /// </summary>
        public ApplicationUser Creator { get; set; } = null!;

        /// <summary>
        /// URL to the group's header image
        /// </summary>
        public string? HeaderImageUrl { get; set; }

        /// <summary>
        /// URL to the group's vertical header image (for mobile/portrait displays)
        /// </summary>
        public string? VerticalHeaderImageUrl { get; set; }

        /// <summary>
        /// Related website URL for the group
        /// </summary>
        public string? WebsiteUrl { get; set; }
    }
}
