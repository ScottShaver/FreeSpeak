using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for Friendship entities.
    /// Provides operations for managing friend requests, friendships, and user blocking.
    /// </summary>
    public class FriendshipRepository : IFriendshipRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FriendshipRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FriendshipRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public FriendshipRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FriendshipRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a friendship by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the friendship.</param>
        /// <returns>The friendship entity with requester and addressee details if found; otherwise, null.</returns>
        public async Task<Friendship?> GetByIdAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .FirstOrDefaultAsync(f => f.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving friendship {FriendshipId}", id);
                return null;
            }
        }

        /// <summary>
        /// Retrieves all friendships in the system.
        /// </summary>
        /// <returns>A list of all friendships with requester and addressee details.</returns>
        public async Task<List<Friendship>> GetAllAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all friendships");
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Adds a new friendship entity to the database.
        /// </summary>
        /// <param name="entity">The friendship entity to add.</param>
        /// <returns>The added friendship entity.</returns>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task<Friendship> AddAsync(Friendship entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Friendships.Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding friendship");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing friendship entity in the database.
        /// </summary>
        /// <param name="entity">The friendship entity with updated values.</param>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task UpdateAsync(Friendship entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Friendships.Update(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating friendship {FriendshipId}", entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a friendship entity from the database.
        /// </summary>
        /// <param name="entity">The friendship entity to delete.</param>
        /// <exception cref="Exception">Thrown when the database operation fails.</exception>
        public async Task DeleteAsync(Friendship entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.Friendships.Remove(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting friendship {FriendshipId}", entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Checks whether a friendship with the specified ID exists.
        /// </summary>
        /// <param name="id">The unique identifier of the friendship.</param>
        /// <returns>True if the friendship exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships.AnyAsync(f => f.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of friendship {FriendshipId}", id);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the friendship between two users, regardless of who initiated the request.
        /// </summary>
        /// <param name="userId1">The unique identifier of the first user.</param>
        /// <param name="userId2">The unique identifier of the second user.</param>
        /// <returns>The friendship entity if found; otherwise, null.</returns>
        public async Task<Friendship?> GetFriendshipAsync(string userId1, string userId2)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .FirstOrDefaultAsync(f =>
                        (f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                        (f.RequesterId == userId2 && f.AddresseeId == userId1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving friendship between users {UserId1} and {UserId2}", userId1, userId2);
                return null;
            }
        }

        /// <summary>
        /// Retrieves all friendships (any status) associated with a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of all friendships where the user is either requester or addressee.</returns>
        public async Task<List<Friendship>> GetUserFriendshipsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving friendships for user {UserId}", userId);
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Retrieves all accepted friendships for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of accepted friendships where the user is either requester or addressee.</returns>
        public async Task<List<Friendship>> GetAcceptedFriendshipsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) && f.Status == FriendshipStatus.Accepted)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving accepted friendships for user {UserId}", userId);
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Retrieves pending friend requests received by a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user receiving requests.</param>
        /// <returns>A list of pending friendship requests ordered by request date descending.</returns>
        public async Task<List<Friendship>> GetPendingRequestsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
                    .OrderByDescending(f => f.RequestedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending requests for user {UserId}", userId);
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Retrieves pending friend requests sent by a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user who sent requests.</param>
        /// <returns>A list of pending friendship requests sent by the user, ordered by request date descending.</returns>
        public async Task<List<Friendship>> GetSentRequestsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Pending)
                    .OrderByDescending(f => f.RequestedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sent requests for user {UserId}", userId);
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Retrieves all users blocked by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user who blocked others.</param>
        /// <returns>A list of friendships with blocked status initiated by the user.</returns>
        public async Task<List<Friendship>> GetBlockedUsersAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships
                    .Include(f => f.Requester)
                    .Include(f => f.Addressee)
                    .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Blocked)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving blocked users for user {UserId}", userId);
                return new List<Friendship>();
            }
        }

        /// <summary>
        /// Checks whether two users are friends (have an accepted friendship).
        /// </summary>
        /// <param name="userId1">The unique identifier of the first user.</param>
        /// <param name="userId2">The unique identifier of the second user.</param>
        /// <returns>True if the users are friends; otherwise, false.</returns>
        public async Task<bool> AreFriendsAsync(string userId1, string userId2)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships.AnyAsync(f =>
                    ((f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                     (f.RequesterId == userId2 && f.AddresseeId == userId1)) &&
                    f.Status == FriendshipStatus.Accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if users {UserId1} and {UserId2} are friends", userId1, userId2);
                return false;
            }
        }

        /// <summary>
        /// Checks whether one user has blocked another.
        /// </summary>
        /// <param name="blockerId">The unique identifier of the user who may have blocked.</param>
        /// <param name="blockedId">The unique identifier of the user who may be blocked.</param>
        /// <returns>True if the blocker has blocked the other user; otherwise, false.</returns>
        public async Task<bool> IsBlockedAsync(string blockerId, string blockedId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships.AnyAsync(f =>
                    f.RequesterId == blockerId && f.AddresseeId == blockedId && f.Status == FriendshipStatus.Blocked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {BlockerId} blocked {BlockedId}", blockerId, blockedId);
                return false;
            }
        }

        /// <summary>
        /// Gets the total count of accepted friends for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The total number of friends the user has.</returns>
        public async Task<int> GetFriendCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships.CountAsync(f =>
                    (f.RequesterId == userId || f.AddresseeId == userId) && f.Status == FriendshipStatus.Accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting friends for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of pending friend requests received by a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The number of pending friend requests awaiting the user's response.</returns>
        public async Task<int> GetPendingRequestCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Friendships.CountAsync(f =>
                    f.AddresseeId == userId && f.Status == FriendshipStatus.Pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting pending requests for user {UserId}", userId);
                return 0;
            }
        }
    }
}
