using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class FriendsService
    {
        private readonly IFriendshipRepository _friendshipRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly NotificationService _notificationService;
        private readonly UserPreferenceService _userPreferenceService;

        public FriendsService(
            IFriendshipRepository friendshipRepository,
            IUserRepository userRepository,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            NotificationService notificationService,
            UserPreferenceService userPreferenceService)
        {
            _friendshipRepository = friendshipRepository;
            _userRepository = userRepository;
            _contextFactory = contextFactory;
            _notificationService = notificationService;
            _userPreferenceService = userPreferenceService;
        }

        /// <summary>
        /// Send a friend request from one user to another
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SendFriendRequestAsync(string requesterId, string addresseeId)
        {
            if (requesterId == addresseeId)
            {
                return (false, "You cannot send a friend request to yourself.");
            }

            // Check if a friendship already exists between these users
            var existingFriendship = await _friendshipRepository.GetFriendshipAsync(requesterId, addresseeId);

            if (existingFriendship != null)
            {
                return existingFriendship.Status switch
                {
                    FriendshipStatus.Pending => (false, "A friend request is already pending between you and this user."),
                    FriendshipStatus.Accepted => (false, "You are already friends with this user."),
                    FriendshipStatus.Blocked => (false, "Unable to send friend request to this user."),
                    FriendshipStatus.Rejected => (false, "A previous friend request was rejected. Please wait before sending another."),
                    _ => (false, "Unable to send friend request.")
                };
            }

            var friendship = new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = FriendshipStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            await _friendshipRepository.AddAsync(friendship);

            // Get requester info for notification
            var requester = await _userRepository.GetByIdAsync(requesterId);
            if (requester != null)
            {
                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    requester.Id,
                    requester.FirstName ?? string.Empty,
                    requester.LastName ?? string.Empty,
                    requester.UserName ?? "User"
                );
                var message = $"<strong>{formattedName}</strong> sent you a friend request";

                // Create notification for the addressee
                await _notificationService.CreateNotificationAsync(
                    userId: addresseeId,
                    type: NotificationType.FriendRequest,
                    message: message,
                    data: new
                    {
                        NavigationUrl = "/friends?tab=requests",
                        FriendshipId = friendship.Id,
                        RequesterId = requesterId,
                        RequesterName = formattedName,
                        RequesterProfilePicture = requester.ProfilePictureUrl
                    }
                );
            }

            return (true, null);
        }

        /// <summary>
        /// Accept a pending friend request
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AcceptFriendRequestAsync(int friendshipId, string currentUserId)
        {
            var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);

            if (friendship == null)
            {
                return (false, "Friend request not found.");
            }

            if (friendship.AddresseeId != currentUserId)
            {
                return (false, "You are not authorized to accept this friend request.");
            }

            if (friendship.Status != FriendshipStatus.Pending)
            {
                return (false, "This friend request is not pending.");
            }

            friendship.Status = FriendshipStatus.Accepted;
            friendship.RespondedAt = DateTime.UtcNow;

            await _friendshipRepository.UpdateAsync(friendship);

            // Send notification to the original requester
            var acceptor = await _userRepository.GetByIdAsync(currentUserId);
            if (acceptor != null)
            {
                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    acceptor.Id,
                    acceptor.FirstName ?? string.Empty,
                    acceptor.LastName ?? string.Empty,
                    acceptor.UserName ?? "User"
                );
                var message = $"<strong>{formattedName}</strong> accepted your friend request";

                await _notificationService.CreateNotificationAsync(
                    userId: friendship.RequesterId,
                    type: NotificationType.FriendAccepted,
                    message: message,
                    data: new
                    {
                        NavigationUrl = "/friends",
                        FriendshipId = friendship.Id,
                        AcceptorId = currentUserId,
                        RequesterName = formattedName,
                        RequesterProfilePicture = acceptor.ProfilePictureUrl
                    }
                );
            }

            return (true, null);
        }

        /// <summary>
        /// Reject a pending friend request
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RejectFriendRequestAsync(int friendshipId, string currentUserId)
        {
            var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);

            if (friendship == null)
            {
                return (false, "Friend request not found.");
            }

            if (friendship.AddresseeId != currentUserId)
            {
                return (false, "You are not authorized to reject this friend request.");
            }

            if (friendship.Status != FriendshipStatus.Pending)
            {
                return (false, "This friend request is not pending.");
            }

            friendship.Status = FriendshipStatus.Rejected;
            friendship.RespondedAt = DateTime.UtcNow;

            await _friendshipRepository.UpdateAsync(friendship);

            return (true, null);
        }

        /// <summary>
        /// Remove a friendship (unfriend)
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RemoveFriendAsync(int friendshipId, string currentUserId)
        {
            var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);

            if (friendship == null)
            {
                return (false, "Friendship not found.");
            }

            if (friendship.RequesterId != currentUserId && friendship.AddresseeId != currentUserId)
            {
                return (false, "You are not authorized to remove this friendship.");
            }

            await _friendshipRepository.DeleteAsync(friendship);

            return (true, null);
        }

        /// <summary>
        /// Block a user
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> BlockUserAsync(string blockerId, string blockedUserId)
        {
            if (blockerId == blockedUserId)
            {
                return (false, "You cannot block yourself.");
            }

            var existingFriendship = await _friendshipRepository.GetFriendshipAsync(blockerId, blockedUserId);

            if (existingFriendship != null)
            {
                existingFriendship.Status = FriendshipStatus.Blocked;
                existingFriendship.RespondedAt = DateTime.UtcNow;
                await _friendshipRepository.UpdateAsync(existingFriendship);
            }
            else
            {
                var friendship = new Friendship
                {
                    RequesterId = blockerId,
                    AddresseeId = blockedUserId,
                    Status = FriendshipStatus.Blocked,
                    RequestedAt = DateTime.UtcNow,
                    RespondedAt = DateTime.UtcNow
                };

                await _friendshipRepository.AddAsync(friendship);
            }

            return (true, null);
        }

        /// <summary>
        /// Get all accepted friends for a user
        /// </summary>
        public async Task<List<ApplicationUser>> GetFriendsAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var friendships = await context.Friendships
                .Include(f => f.Requester)
                .Include(f => f.Addressee)
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId || f.AddresseeId == userId))
                .ToListAsync();

            var friends = friendships
                .Select(f => f.RequesterId == userId ? f.Addressee : f.Requester)
                .ToList();

            return friends;
        }

        /// <summary>
        /// Get pending friend requests received by a user
        /// </summary>
        public async Task<List<(Friendship Friendship, ApplicationUser Requester)>> GetPendingRequestsAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var requests = await context.Friendships
                .Include(f => f.Requester)
                .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
                .OrderByDescending(f => f.RequestedAt)
                .ToListAsync();

            return requests
                .Where(f => f.Requester != null)
                .Select(f => (f, f.Requester))
                .ToList();
        }

        /// <summary>
        /// Get pending friend requests sent by a user
        /// </summary>
        public async Task<List<(Friendship Friendship, ApplicationUser Addressee)>> GetSentRequestsAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var requests = await context.Friendships
                .Include(f => f.Addressee)
                .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Pending)
                .OrderByDescending(f => f.RequestedAt)
                .ToListAsync();

            return requests
                .Where(f => f.Addressee != null)
                .Select(f => (f, f.Addressee))
                .ToList();
        }

        /// <summary>
        /// Check if two users are friends
        /// </summary>
        public async Task<bool> AreFriendsAsync(string userId1, string userId2)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Friendships
                .AnyAsync(f => f.Status == FriendshipStatus.Accepted &&
                              ((f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                               (f.RequesterId == userId2 && f.AddresseeId == userId1)));
        }

        /// <summary>
        /// Check the friendship status between two users
        /// </summary>
        public async Task<FriendshipStatus?> GetFriendshipStatusAsync(string userId1, string userId2)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var friendship = await context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                    (f.RequesterId == userId2 && f.AddresseeId == userId1));

            return friendship?.Status;
        }

        /// <summary>
        /// Get friendship details between two users
        /// </summary>
        public async Task<Friendship?> GetFriendshipAsync(string userId1, string userId2)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                    (f.RequesterId == userId2 && f.AddresseeId == userId1));
        }

        /// <summary>
        /// Search for users by username, first name, or last name (excluding current user and already connected users)
        /// Multiple search terms are treated separately - each term can match different records
        /// </summary>
        public async Task<List<ApplicationUser>> SearchUsersAsync(string searchTerm, string currentUserId, int maxResults = 20)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get users with existing friendships
            var existingFriendshipUserIds = await context.Friendships
                .Where(f => f.RequesterId == currentUserId || f.AddresseeId == currentUserId)
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Split search term into individual words and normalize for case-insensitive comparison
            var searchTerms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(term => term.ToLower())
                                       .ToList();

            if (!searchTerms.Any())
            {
                return new List<ApplicationUser>();
            }

            var users = await context.Users
                .Where(u => u.Id != currentUserId &&
                           !existingFriendshipUserIds.Contains(u.Id) &&
                           u.UserName != null &&
                           searchTerms.Any(term =>
                               u.UserName.ToLower().Contains(term) ||
                               u.FirstName.ToLower().Contains(term) ||
                               u.LastName.ToLower().Contains(term)))
                .Take(maxResults)
                .ToListAsync();

            return users;
        }

        /// <summary>
        /// Get people you may know based on mutual friends
        /// </summary>
        /// <param name="userId">The current user's ID</param>
        /// <param name="maxResults">Maximum number of suggestions to return (default 50)</param>
        /// <returns>List of suggested users with mutual friend count</returns>
        public async Task<List<(ApplicationUser User, int MutualFriendsCount)>> GetPeopleYouMayKnowAsync(string userId, int maxResults = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get current user's friends
            var userFriendIds = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            if (!userFriendIds.Any())
            {
                return new List<(ApplicationUser, int)>();
            }

            // Get users with existing relationships (friends, pending, blocked, rejected)
            var existingRelationshipUserIds = await context.Friendships
                .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Get friends of friends
            var friendsOfFriends = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           userFriendIds.Contains(f.RequesterId == userId ? f.AddresseeId : f.RequesterId))
                .Select(f => new
                {
                    UserId = userFriendIds.Contains(f.RequesterId) ? f.AddresseeId : f.RequesterId,
                    MutualFriendId = userFriendIds.Contains(f.RequesterId) ? f.RequesterId : f.AddresseeId
                })
                .Where(x => x.UserId != userId && !existingRelationshipUserIds.Contains(x.UserId))
                .ToListAsync();

            // Group by user and count mutual friends
            var suggestions = friendsOfFriends
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    MutualFriendsCount = g.Count()
                })
                .OrderByDescending(x => x.MutualFriendsCount)
                .Take(maxResults)
                .ToList();

            // Load the actual user objects
            var suggestedUserIds = suggestions.Select(s => s.UserId).ToList();
            var users = await context.Users
                .Where(u => suggestedUserIds.Contains(u.Id))
                .ToListAsync();

            // Combine users with their mutual friend counts
            var result = suggestions
                .Select(s => (
                    User: users.First(u => u.Id == s.UserId),
                    MutualFriendsCount: s.MutualFriendsCount
                ))
                .ToList();

            return result;
        }

        /// <summary>
        /// Get the list of mutual friends between two users
        /// </summary>
        /// <param name="userId1">First user ID</param>
        /// <param name="userId2">Second user ID</param>
        /// <returns>List of users who are friends with both users</returns>
        public async Task<List<ApplicationUser>> GetMutualFriendsAsync(string userId1, string userId2)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get friends of user 1
            var user1FriendIds = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId1 || f.AddresseeId == userId1))
                .Select(f => f.RequesterId == userId1 ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Get friends of user 2
            var user2FriendIds = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId2 || f.AddresseeId == userId2))
                .Select(f => f.RequesterId == userId2 ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Find common friends (intersection)
            var mutualFriendIds = user1FriendIds.Intersect(user2FriendIds).ToList();

            if (!mutualFriendIds.Any())
            {
                return new List<ApplicationUser>();
            }

            // Load the actual user objects
            var mutualFriends = await context.Users
                .Where(u => mutualFriendIds.Contains(u.Id))
                .ToListAsync();

            return mutualFriends;
        }

        /// <summary>
        /// Get count of accepted friends for a user
        /// </summary>
        public async Task<int> GetFriendsCountAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId || f.AddresseeId == userId))
                .CountAsync();
        }

        /// <summary>
        /// Get count of pending friend requests received by a user
        /// </summary>
        public async Task<int> GetPendingRequestsCountAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Friendships
                .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
                .CountAsync();
        }

        /// <summary>
        /// Get count of pending friend requests sent by a user
        /// </summary>
        public async Task<int> GetSentRequestsCountAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Friendships
                .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Pending)
                .CountAsync();
        }

        /// <summary>
        /// Get count of people you may know suggestions
        /// </summary>
        public async Task<int> GetPeopleYouMayKnowCountAsync(string userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get current user's friends
            var userFriendIds = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            if (!userFriendIds.Any())
            {
                return 0;
            }

            // Get users with existing relationships (friends, pending, blocked, rejected)
            var existingRelationshipUserIds = await context.Friendships
                .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
                .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            // Get friends of friends
            var friendsOfFriendsCount = await context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                           userFriendIds.Contains(f.RequesterId == userId ? f.AddresseeId : f.RequesterId))
                .Select(f => userFriendIds.Contains(f.RequesterId) ? f.AddresseeId : f.RequesterId)
                .Where(x => x != userId && !existingRelationshipUserIds.Contains(x))
                .Distinct()
                .CountAsync();

            return friendsOfFriendsCount;
        }
    }
}
