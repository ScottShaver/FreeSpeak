namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a rule for a group that members must follow
    /// </summary>
    public class GroupRule
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the group this rule belongs to
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Navigation property to the group
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// The title of the rule
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Detailed description of the rule
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Order in which the rule should be displayed (lower numbers first)
        /// </summary>
        public int Order { get; set; }
    }
}
