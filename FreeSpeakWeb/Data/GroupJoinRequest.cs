namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a request from a user to join a group
    /// </summary>
    public class GroupJoinRequest
    {
        /// <summary>
        /// Unique identifier for the join request
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the group the user wants to join
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Navigation property to the group
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// The ID of the user requesting to join
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Navigation property to the user
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// When the join request was created
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }
}
