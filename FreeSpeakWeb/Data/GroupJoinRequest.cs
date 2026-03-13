namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user's request to join a group that requires join approval.
    /// Pending requests must be approved by a group administrator before the user can join.
    /// </summary>
    public class GroupJoinRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for the join request.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group the user wants to join.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user requesting to join the group.
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the requesting user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the timestamp when the join request was created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }
}
