namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a community group where users can join to share and discuss content about a specific topic.
    /// Groups can be public or private, require join approval, and support custom rules and branding.
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Gets or sets the unique identifier for the group.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the group.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the description explaining the group's purpose and topic.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the group was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether this group is publicly visible to all users.
        /// Private groups are only visible to members. Defaults to true.
        /// </summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this group is hidden from search results and discovery pages.
        /// Hidden groups can only be joined via direct invitation. Defaults to false.
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// Gets or sets the timestamp of the last activity in the group (most recent post or interaction).
        /// Used for sorting groups by activity. Defaults to UTC now.
        /// </summary>
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the total number of members in the group.
        /// Updated when users join or leave. Defaults to 0.
        /// </summary>
        public int MemberCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether new members need administrator approval before joining.
        /// When true, users must submit a join request. Defaults to false.
        /// </summary>
        public bool RequiresJoinApproval { get; set; } = false;

        /// <summary>
        /// Gets or sets whether users must accept the group rules before joining.
        /// When true, users must agree to the group's rules (from GroupRules table) before joining or submitting a join request. Defaults to false.
        /// </summary>
        public bool RequireAcceptRules { get; set; } = false;

        /// <summary>
        /// Gets or sets whether new posts require moderator approval before being visible to members.
        /// When true, all new posts start with Pending status and must be approved by a moderator. Defaults to false.
        /// </summary>
        public bool RequiresPostApproval { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the group points system is enabled for this group.
        /// When true, members earn points for posts, comments, likes, and milestones. Group admins can toggle this feature. Defaults to false.
        /// </summary>
        public bool EnablePointsSystem { get; set; } = false;

        /// <summary>
        /// Gets or sets whether file uploads are enabled for this group.
        /// When true, members can upload files to share with other group members. Group admins can toggle this feature. Defaults to false.
        /// </summary>
        public bool EnableFileUploads { get; set; } = false;

        /// <summary>
        /// Gets or sets whether uploaded files require moderator or administrator approval before being visible to members.
        /// When true, all file uploads start with Pending status and must be approved before other members can see or download them. Defaults to false.
        /// </summary>
        public bool RequiresFileApproval { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the group is active and ready to accept users and content.
        /// When false, the group is in setup mode and not visible to non-admin users. Defaults to false.
        /// </summary>
        public bool IsActive { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the group has been permanently closed.
        /// When true, the group is completely shut down and no changes can be made, even by administrators. Defaults to false.
        /// </summary>
        public bool IsClosed { get; set; } = false;

        /// <summary>
        /// Gets or sets the ID of the user who created this group.
        /// The creator typically has full administrative permissions.
        /// </summary>
        public required string CreatorId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who created this group.
        /// </summary>
        public ApplicationUser Creator { get; set; } = null!;

        /// <summary>
        /// Gets or sets the URL to the group's horizontal header banner image.
        /// Displayed at the top of the group page on desktop views.
        /// </summary>
        public string? HeaderImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to the group's vertical header image.
        /// Used for mobile and portrait display orientations.
        /// </summary>
        public string? VerticalHeaderImageUrl { get; set; }

        /// <summary>
        /// Gets or sets an optional external website URL related to the group.
        /// Can be used to link to the group's official site or related resources.
        /// </summary>
        public string? WebsiteUrl { get; set; }
    }
}
