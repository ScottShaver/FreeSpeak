using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for Friendship entity
    /// </summary>
    public class FriendshipRepository : IFriendshipRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FriendshipRepository> _logger;

        public FriendshipRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FriendshipRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

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
