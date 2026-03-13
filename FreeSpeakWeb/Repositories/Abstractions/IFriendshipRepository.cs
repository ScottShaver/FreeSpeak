using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing Friendship entities.
    /// Handles friend requests, acceptances, rejections, and blocks between users.
    /// Supports bidirectional friendship queries regardless of who initiated the relationship.
    /// </summary>
    public interface IFriendshipRepository : IRepository<Friendship>
    {
        /// <summary>
        /// Retrieves the friendship relationship between two users, regardless of who initiated it.
        /// </summary>
        /// <param name="userId1">The ID of the first user.</param>
        /// <param name="userId2">The ID of the second user.</param>
        /// <returns>The friendship if one exists; otherwise, null.</returns>
        Task<Friendship?> GetFriendshipAsync(string userId1, string userId2);

        /// <summary>
        /// Retrieves all friendships for a user in any status (accepted, pending, rejected, blocked).
        /// Includes friendships where the user is either the requester or the addressee.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of all friendships involving the user.</returns>
        Task<List<Friendship>> GetUserFriendshipsAsync(string userId);

        /// <summary>
        /// Retrieves all accepted friendships for a user.
        /// Returns only friendships with FriendshipStatus.Accepted.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of accepted friendships.</returns>
        Task<List<Friendship>> GetAcceptedFriendshipsAsync(string userId);

        /// <summary>
        /// Retrieves pending friend requests received by a user.
        /// Returns friendships where the user is the addressee and status is Pending.
        /// </summary>
        /// <param name="userId">The ID of the user receiving the requests.</param>
        /// <returns>A list of pending incoming friend requests.</returns>
        Task<List<Friendship>> GetPendingRequestsAsync(string userId);

        /// <summary>
        /// Retrieves pending friend requests sent by a user.
        /// Returns friendships where the user is the requester and status is Pending.
        /// </summary>
        /// <param name="userId">The ID of the user who sent the requests.</param>
        /// <returns>A list of pending outgoing friend requests.</returns>
        Task<List<Friendship>> GetSentRequestsAsync(string userId);

        /// <summary>
        /// Retrieves users that have been blocked by a specific user.
        /// Returns friendships where status is Blocked.
        /// </summary>
        /// <param name="userId">The ID of the user who created the blocks.</param>
        /// <returns>A list of blocked user relationships.</returns>
        Task<List<Friendship>> GetBlockedUsersAsync(string userId);

        /// <summary>
        /// Checks whether two users are friends (friendship is accepted).
        /// </summary>
        /// <param name="userId1">The ID of the first user.</param>
        /// <param name="userId2">The ID of the second user.</param>
        /// <returns>True if the users are friends; otherwise, false.</returns>
        Task<bool> AreFriendsAsync(string userId1, string userId2);

        /// <summary>
        /// Checks whether one user has blocked another user.
        /// </summary>
        /// <param name="blockerId">The ID of the user who may have created the block.</param>
        /// <param name="blockedId">The ID of the user who may be blocked.</param>
        /// <returns>True if the blocker has blocked the other user; otherwise, false.</returns>
        Task<bool> IsBlockedAsync(string blockerId, string blockedId);

        /// <summary>
        /// Gets the total count of accepted friendships for a user.
        /// Used for displaying friend count on user profiles.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The number of accepted friendships.</returns>
        Task<int> GetFriendCountAsync(string userId);

        /// <summary>
        /// Gets the count of pending friend requests received by a user.
        /// Used for notification badge display.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The number of pending incoming friend requests.</returns>
        Task<int> GetPendingRequestCountAsync(string userId);
    }
}
