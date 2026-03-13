namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents the status of a friendship relationship between two users.
    /// </summary>
    public enum FriendshipStatus
    {
        /// <summary>
        /// Friend request has been sent but not yet responded to.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Friend request has been accepted; users are now friends.
        /// </summary>
        Accepted = 1,

        /// <summary>
        /// Friend request has been rejected.
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// One user has blocked the other, preventing any interaction.
        /// </summary>
        Blocked = 3
    }

    /// <summary>
    /// Represents a friendship relationship or friend request between two users.
    /// Tracks the requester, addressee, status, and timestamps of the relationship.
    /// </summary>
    public class Friendship
    {
        /// <summary>
        /// Gets or sets the unique identifier for the friendship record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who initiated the friend request.
        /// </summary>
        public required string RequesterId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who initiated the request.
        /// </summary>
        public ApplicationUser Requester { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who received the friend request.
        /// </summary>
        public required string AddresseeId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who received the request.
        /// </summary>
        public ApplicationUser Addressee { get; set; } = null!;

        /// <summary>
        /// Gets or sets the current status of the friendship (Pending, Accepted, Rejected, Blocked).
        /// Defaults to Pending.
        /// </summary>
        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

        /// <summary>
        /// Gets or sets the timestamp when the friend request was sent.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when the friend request was responded to (accepted, rejected, or blocked).
        /// Null if the request is still pending.
        /// </summary>
        public DateTime? RespondedAt { get; set; }

        /// <summary>
        /// Checks if the friendship is between two specific users, regardless of who initiated the request.
        /// </summary>
        /// <param name="userId1">The ID of the first user.</param>
        /// <param name="userId2">The ID of the second user.</param>
        /// <returns>True if the friendship involves both specified users; otherwise, false.</returns>
        public bool IsBetween(string userId1, string userId2)
        {
            return (RequesterId == userId1 && AddresseeId == userId2) ||
                   (RequesterId == userId2 && AddresseeId == userId1);
        }

        /// <summary>
        /// Gets the ID of the other user in the friendship relationship.
        /// </summary>
        /// <param name="userId">The ID of one user in the friendship.</param>
        /// <returns>The ID of the other user in the friendship.</returns>
        public string GetOtherUserId(string userId)
        {
            return userId == RequesterId ? AddresseeId : RequesterId;
        }
    }
}
