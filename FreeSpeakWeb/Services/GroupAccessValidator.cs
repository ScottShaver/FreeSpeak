using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Centralized helper service for validating user access to groups.
    /// Used by GroupPostService and other group-related services to eliminate validation code duplication.
    /// </summary>
    public class GroupAccessValidator
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupAccessValidator> _logger;
        private readonly IRoleService _roleService;

        public GroupAccessValidator(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupAccessValidator> logger,
            IRoleService roleService)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _roleService = roleService;
        }

        /// <summary>
        /// Validates if a user has access to a group (is a member and not banned)
        /// </summary>
        /// <returns>Tuple with IsMember, IsBanned status</returns>
        public async Task<(bool IsMember, bool IsBanned)> ValidateUserAccessAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (!isMember)
                {
                    return (false, false);
                }

                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

                return (true, isBanned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user {UserId} access to group {GroupId}", userId, groupId);
                return (false, false);
            }
        }

        /// <summary>
        /// Validates whether a user may read a group's content (posts, comments, authors).
        /// Banned users are always denied, even for public groups. Private groups require membership.
        /// Anonymous users (null/empty userId) may only read public groups.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="isPublic">Whether the group is public.</param>
        /// <param name="userId">The current user's identifier, or null when anonymous.</param>
        /// <returns>Success flag and, when denied, a user-facing message.</returns>
        public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanViewAsync(int groupId, bool isPublic, string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return isPublic
                    ? (true, null)
                    : (false, "You must be logged in to view posts in this private group.");
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

                if (isBanned)
                {
                    return (false, "You have been banned from this group and can no longer view its content.");
                }

                if (!isPublic)
                {
                    var isMember = await context.GroupUsers
                        .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                    if (!isMember)
                    {
                        return (false, "You must be a member of this group to view this post.");
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating view access for user {UserId} to group {GroupId}", userId, groupId);
                return (false, "Unable to verify access to this group.");
            }
        }

        /// <summary>
        /// Checks if a user is an admin or moderator of a group, or a system administrator.
        /// </summary>
        public async Task<bool> IsGroupAdminOrModeratorAsync(int groupId, string userId)
        {
            try
            {
                // System administrators have full access to all groups
                var isSystemAdmin = await _roleService.IsSystemAdministratorAsync(userId);
                if (isSystemAdmin)
                {
                    return true;
                }

                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && 
                                   gu.UserId == userId && 
                                   (gu.IsAdmin || gu.IsModerator));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin/moderator status for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Validates if a user can perform actions in a group (posting, commenting, liking)
        /// Returns success/error tuple suitable for direct return from service methods
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanActAsync(int groupId, string userId)
        {
            var (isMember, isBanned) = await ValidateUserAccessAsync(groupId, userId);

            if (!isMember)
            {
                return (false, "You must be a member of the group to perform this action.");
            }

            if (isBanned)
            {
                return (false, "You are banned from this group.");
            }

            return (true, null);
        }

        /// <summary>
        /// Validates if a user can post in a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanPostAsync(int groupId, string userId)
        {
            var (isMember, isBanned) = await ValidateUserAccessAsync(groupId, userId);

            if (!isMember)
            {
                return (false, "You must be a member of the group to post.");
            }

            if (isBanned)
            {
                return (false, "You are banned from this group.");
            }

            return (true, null);
        }

        /// <summary>
        /// Validates if a user can delete content (is author OR admin/moderator)
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanDeleteAsync(
            int groupId, 
            string userId, 
            string contentAuthorId)
        {
            // Author can always delete their own content
            if (userId == contentAuthorId)
            {
                return (true, null);
            }

            // Check if user is admin or moderator
            var isAdminOrModerator = await IsGroupAdminOrModeratorAsync(groupId, userId);

            if (isAdminOrModerator)
            {
                return (true, null);
            }

            return (false, "You are not authorized to delete this content.");
        }
    }
}
