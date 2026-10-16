using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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
        private readonly IStringLocalizer<GroupAccessValidator> _localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupAccessValidator"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording validation failures.</param>
        /// <param name="roleService">Service for role operations.</param>
        /// <param name="localizer">Optional localizer for user-facing result messages; when omitted the neutral (English) text is used.</param>

        public GroupAccessValidator(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupAccessValidator> logger,
            IRoleService roleService,
            IStringLocalizer<GroupAccessValidator>? localizer = null)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _roleService = roleService;
            _localizer = ServiceLocalizer.Resolve(localizer);
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
                    : (false, _localizer["LoginRequiredPrivateGroup"].Value);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

                if (isBanned)
                {
                    return (false, _localizer["BannedCannotViewContent"].Value);
                }

                if (!isPublic)
                {
                    var isMember = await context.GroupUsers
                        .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                    if (!isMember)
                    {
                        return (false, _localizer["MustBeMemberToViewPost"].Value);
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating view access for user {UserId} to group {GroupId}", userId, groupId);
                return (false, _localizer["UnableToVerifyAccess"].Value);
            }
        }

        /// <summary>
        /// Validates whether a user may open a group's home page.
        /// System administrators may view any group for moderation purposes; everyone else
        /// is subject to <see cref="ValidateUserCanViewAsync"/> (public groups are viewable by non-members,
        /// private groups require membership, banned users are always denied).
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="isPublic">Whether the group is public.</param>
        /// <param name="userId">The current user's identifier.</param>
        /// <returns>Success flag and, when denied, a user-facing message.</returns>
        public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanViewGroupPageAsync(int groupId, bool isPublic, string? userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    if (await _roleService.IsSystemAdministratorAsync(userId))
                    {
                        return (true, null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking system administrator status for user {UserId}", userId);
                    return (false, _localizer["UnableToVerifyAccess"].Value);
                }
            }

            return await ValidateUserCanViewAsync(groupId, isPublic, userId);
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

                // System moderators act as a backstop for group moderation
                if (await _roleService.IsUserInRoleAsync(userId, "SystemModerator"))
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
                return (false, _localizer["MustBeMemberToPerformAction"].Value);
            }

            if (isBanned)
            {
                return (false, _localizer["BannedFromGroup"].Value);
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
                return (false, _localizer["MustBeMemberToPost"].Value);
            }

            if (isBanned)
            {
                return (false, _localizer["BannedFromGroup"].Value);
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

            return (false, _localizer["NotAuthorizedToDelete"].Value);
        }
    }
}
