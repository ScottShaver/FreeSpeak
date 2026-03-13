using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for Friendship entity
    /// </summary>
    public interface IFriendshipRepository : IRepository<Friendship>
    {
        /// <summary>
        /// Get a friendship between two users regardless of direction
        /// </summary>
        Task<Friendship?> GetFriendshipAsync(string userId1, string userId2);

        /// <summary>
        /// Get all friendships for a user (both as requester and addressee)
        /// </summary>
        Task<List<Friendship>> GetUserFriendshipsAsync(string userId);

        /// <summary>
        /// Get all accepted friendships for a user
        /// </summary>
        Task<List<Friendship>> GetAcceptedFriendshipsAsync(string userId);

        /// <summary>
        /// Get pending friend requests received by a user
        /// </summary>
        Task<List<Friendship>> GetPendingRequestsAsync(string userId);

        /// <summary>
        /// Get pending friend requests sent by a user
        /// </summary>
        Task<List<Friendship>> GetSentRequestsAsync(string userId);

        /// <summary>
        /// Get blocked users for a user
        /// </summary>
        Task<List<Friendship>> GetBlockedUsersAsync(string userId);

        /// <summary>
        /// Check if two users are friends
        /// </summary>
        Task<bool> AreFriendsAsync(string userId1, string userId2);

        /// <summary>
        /// Check if user is blocked by another user
        /// </summary>
        Task<bool> IsBlockedAsync(string blockerId, string blockedId);

        /// <summary>
        /// Get count of accepted friendships for a user
        /// </summary>
        Task<int> GetFriendCountAsync(string userId);

        /// <summary>
        /// Get count of pending requests for a user
        /// </summary>
        Task<int> GetPendingRequestCountAsync(string userId);
    }
}
