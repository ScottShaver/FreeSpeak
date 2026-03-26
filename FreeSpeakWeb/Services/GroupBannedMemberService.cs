using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing banned members in groups.
    /// Handles banning, unbanning, and checking ban status of users.
    /// </summary>
    public class GroupBannedMemberService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupBannedMemberService> _logger;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IRoleService _roleService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupBannedMemberService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        /// <param name="roleService">Service for role operations.</param>
        public GroupBannedMemberService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupBannedMemberService> logger,
            IAuditLogRepository auditLogRepository,
            IRoleService roleService)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
            _roleService = roleService;
        }

        /// <summary>
        /// Ban a user from a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> BanUserAsync(int groupId, string userId, string bannedByUserId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if banner is a system administrator
                var isSystemAdmin = await _roleService.IsSystemAdministratorAsync(bannedByUserId);

                // Verify the banner is an admin, moderator, or system administrator
                var bannerMembership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == bannedByUserId);

                if (!isSystemAdmin && (bannerMembership == null || (!bannerMembership.IsAdmin && !bannerMembership.IsModerator)))
                {
                    return (false, "You must be an admin or moderator to ban users.");
                }

                // Verify the user to be banned is a member
                var userMembership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (userMembership == null)
                {
                    return (false, "User is not a member of this group.");
                }

                // Prevent banning the group creator
                var group = await context.Groups.FindAsync(groupId);
                if (group != null && group.CreatorId == userId)
                {
                    return (false, "Cannot ban the group creator.");
                }

                // Prevent regular moderators from banning admins (unless banner is system admin)
                if (!isSystemAdmin && userMembership.IsAdmin && bannerMembership != null && !bannerMembership.IsAdmin)
                {
                    return (false, "Moderators cannot ban administrators.");
                }

                // Check if already banned
                var existingBan = await context.GroupBannedMembers
                    .FirstOrDefaultAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

                if (existingBan != null)
                {
                    return (false, "User is already banned from this group.");
                }

                // Get user details for audit log
                var bannedUser = await context.Users.FindAsync(userId);

                // Create ban record
                var bannedMember = new GroupBannedMember
                {
                    GroupId = groupId,
                    UserId = userId,
                    BannedAt = DateTime.UtcNow
                };

                context.GroupBannedMembers.Add(bannedMember);

                // Remove the user from the group
                context.GroupUsers.Remove(userMembership);

                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} banned from group {GroupId} by {BannedByUserId}", userId, groupId, bannedByUserId);

                // Log ban action to audit log
                await _auditLogRepository.LogActionAsync(bannedByUserId, ActionCategory.GroupAdminBanUser, new GroupAdminBanUserDetails
                {
                    GroupId = groupId,
                    GroupName = group?.Name ?? string.Empty,
                    BannedUserId = userId,
                    BannedUserDisplayName = bannedUser != null ? $"{bannedUser.FirstName} {bannedUser.LastName}" : null,
                    IsPermanent = true
                });

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning user {UserId} from group {GroupId}", userId, groupId);
                return (false, "An error occurred while banning the user.");
            }
        }

        /// <summary>
        /// Unban a user from a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UnbanUserAsync(int groupId, string userId, string unbannedByUserId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if unbanner is a system administrator
                var isSystemAdmin = await _roleService.IsSystemAdministratorAsync(unbannedByUserId);

                // Verify the unbanner is an admin, moderator, or system administrator
                var unbannerMembership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == unbannedByUserId);

                if (!isSystemAdmin && (unbannerMembership == null || (!unbannerMembership.IsAdmin && !unbannerMembership.IsModerator)))
                {
                    return (false, "You must be an admin or moderator to unban users.");
                }

                // Find the ban record
                var bannedMember = await context.GroupBannedMembers
                    .FirstOrDefaultAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

                if (bannedMember == null)
                {
                    return (false, "User is not banned from this group.");
                }

                // Remove the ban
                context.GroupBannedMembers.Remove(bannedMember);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unbanned from group {GroupId} by {UnbannedByUserId}", userId, groupId, unbannedByUserId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user {UserId} from group {GroupId}", userId, groupId);
                return (false, "An error occurred while unbanning the user.");
            }
        }

        /// <summary>
        /// Check if a user is banned from a group
        /// </summary>
        public async Task<bool> IsUserBannedAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is banned from group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Get all banned members for a group
        /// </summary>
        public async Task<List<GroupBannedMember>> GetBannedMembersAsync(int groupId, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupBannedMembers
                    .Include(gbm => gbm.User)
                    .Where(gbm => gbm.GroupId == groupId)
                    .OrderByDescending(gbm => gbm.BannedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving banned members for group {GroupId}", groupId);
                return new List<GroupBannedMember>();
            }
        }

        /// <summary>
        /// Get the count of banned members for a group
        /// </summary>
        public async Task<int> GetBannedMemberCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupBannedMembers
                    .CountAsync(gbm => gbm.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting banned members for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <summary>
        /// Get all groups a user is banned from
        /// </summary>
        public async Task<List<GroupBannedMember>> GetUserBansAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupBannedMembers
                    .Include(gbm => gbm.Group)
                    .Where(gbm => gbm.UserId == userId)
                    .OrderByDescending(gbm => gbm.BannedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bans for user {UserId}", userId);
                return new List<GroupBannedMember>();
            }
        }
    }
}
