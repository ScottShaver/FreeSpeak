namespace FreeSpeakWeb.Data
{
    public enum FriendshipStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Blocked = 3
    }

    public class Friendship
    {
        public int Id { get; set; }

        /// <summary>
        /// The user who initiated the friend request
        /// </summary>
        public required string RequesterId { get; set; }
        public ApplicationUser Requester { get; set; } = null!;

        /// <summary>
        /// The user who received the friend request
        /// </summary>
        public required string AddresseeId { get; set; }
        public ApplicationUser Addressee { get; set; } = null!;

        public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        /// <summary>
        /// Check if the friendship is between two specific users (regardless of who initiated)
        /// </summary>
        public bool IsBetween(string userId1, string userId2)
        {
            return (RequesterId == userId1 && AddresseeId == userId2) ||
                   (RequesterId == userId2 && AddresseeId == userId1);
        }

        /// <summary>
        /// Get the other user in the friendship
        /// </summary>
        public string GetOtherUserId(string userId)
        {
            return userId == RequesterId ? AddresseeId : RequesterId;
        }
    }
}
