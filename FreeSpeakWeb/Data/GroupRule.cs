namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a rule that group members must follow.
    /// Rules are displayed in order and help establish community guidelines.
    /// </summary>
    public class GroupRule
    {
        /// <summary>
        /// Gets or sets the unique identifier for the rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group this rule belongs to.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the title of the rule.
        /// Should be a brief, clear statement of the rule.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Gets or sets the detailed description of the rule.
        /// Explains the rule's purpose and what behavior is expected or prohibited.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Gets or sets the display order for the rule.
        /// Rules with lower order values are displayed first.
        /// </summary>
        public int Order { get; set; }
    }
}
