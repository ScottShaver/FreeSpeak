using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Services
{
    public class GroupMemberService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IGroupMemberRepository _memberRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly ILogger<GroupMemberService> _logger;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly NotificationService _notificationService;
        private readonly IRoleService _roleService;
        private readonly IGroupModerationLogService? _moderationLog;
        private readonly IStringLocalizer<Resources.Services.GroupMemberService> _localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupMemberService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="memberRepository">Repository for group member operations.</param>
        /// <param name="groupRepository">Repository for group operations.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        /// <param name="notificationService">Service for sending notifications.</param>
        /// <param name="roleService">Service for role operations.</param>
        /// <param name="moderationLog">Optional per-group moderation log; when omitted nothing is recorded there.</param>
        /// <param name="localizer">Optional localizer for user-facing result messages; when omitted the neutral (English) text is used.</param>
        public GroupMemberService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IGroupMemberRepository memberRepository,
            IGroupRepository groupRepository,
            ILogger<GroupMemberService> logger,
            IAuditLogRepository auditLogRepository,
            NotificationService notificationService,
            IRoleService roleService,
            IGroupModerationLogService? moderationLog = null,
            IStringLocalizer<Resources.Services.GroupMemberService>? localizer = null)
        {
            _contextFactory = contextFactory;
            _memberRepository = memberRepository;
            _groupRepository = groupRepository;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
            _notificationService = notificationService;
            _roleService = roleService;
            _moderationLog = moderationLog;
            _localizer = ServiceLocalizer.Resolve(localizer);
        }

        /// <summary>
        /// Records a member-related action in the group moderation log. Logging failures never affect the caller.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="actorUserId">The admin or moderator who acted.</param>
        /// <param name="action">The action taken.</param>
        /// <param name="targetUserId">The affected member.</param>
        /// <param name="summary">Optional short plain-text detail, such as the old and new role.</param>
        private async Task LogModerationAsync(int groupId, string actorUserId, GroupModerationAction action, string targetUserId, string? summary = null)
        {
            if (_moderationLog == null)
            {
                return;
            }

            await _moderationLog.LogAsync(new GroupModerationLogRequest
            {
                GroupId = groupId,
                ActorUserId = actorUserId,
                Action = action,
                TargetType = GroupModerationTargetType.Member,
                TargetId = targetUserId,
                TargetUserId = targetUserId,
                Summary = summary
            });
        }

        #region Join Requests

        /// <summary>
        /// Create a join request for a group
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user requesting to join.</param>
        /// <param name="hasAgreedToRules">Whether the user has agreed to the group rules (required if group has RequireAcceptRules set).</param>
        public async Task<(bool Success, string? ErrorMessage, GroupJoinRequest? Request)> CreateJoinRequestAsync(
            int groupId,
            string userId,
            bool hasAgreedToRules = false)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, _localizer["UserIdRequired"].Value, null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify group exists
                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value, null);
                }

                // Check if group requires rules acceptance
                if (group.RequireAcceptRules && !hasAgreedToRules)
                {
                    return (false, _localizer["MustAgreeToRulesBeforeJoinRequest"].Value, null);
                }

                // Check if user is already a member
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
                if (isMember)
                {
                    return (false, _localizer["AlreadyMember"].Value, null);
                }

                // Check if user is banned from the group
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);
                if (isBanned)
                {
                    return (false, _localizer["BannedCannotRequestJoin"].Value, null);
                }

                // Check if there's already a pending request
                var existingRequest = await context.GroupJoinRequests
                    .FirstOrDefaultAsync(jr => jr.GroupId == groupId && jr.UserId == userId);
                if (existingRequest != null)
                {
                    return (false, _localizer["PendingRequestExists"].Value, null);
                }

                var request = new GroupJoinRequest
                {
                    GroupId = groupId,
                    UserId = userId,
                    RequestedAt = DateTime.UtcNow
                };

                context.GroupJoinRequests.Add(request);
                await context.SaveChangesAsync();

                _logger.LogInformation("Join request {RequestId} created for group {GroupId} by user {UserId} with rules agreement: {HasAgreedToRules}", 
                    request.Id, groupId, userId, hasAgreedToRules);

                // Log join request to audit log
                await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserGroupRequests, new UserGroupRequestsDetails
                {
                    OperationType = OperationTypeEnum.RequestSubmit.ToString(),
                    GroupId = groupId,
                    GroupName = group.Name,
                    RequiresApproval = true
                });

                return (true, null, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating join request for group {GroupId} by user {UserId}", groupId, userId);
                return (false, _localizer["ErrorCreatingJoinRequest"].Value, null);
            }
        }

        /// <summary>
        /// Get pending join requests for a group
        /// </summary>
        public async Task<List<GroupJoinRequest>> GetPendingJoinRequestsAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupJoinRequests
                    .Include(jr => jr.User)
                    .Where(jr => jr.GroupId == groupId)
                    .OrderBy(jr => jr.RequestedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving join requests for group {GroupId}", groupId);
                return new List<GroupJoinRequest>();
            }
        }

        /// <summary>
        /// Get all pending join requests sent by a user
        /// </summary>
        public async Task<List<GroupJoinRequest>> GetUserJoinRequestsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupJoinRequests
                    .Include(jr => jr.Group)
                    .Where(jr => jr.UserId == userId)
                    .OrderByDescending(jr => jr.RequestedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving join requests for user {UserId}", userId);
                return new List<GroupJoinRequest>();
            }
        }

        /// <summary>
        /// Approve a join request
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ApproveJoinRequestAsync(
            int requestId,
            string approverId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var request = await context.GroupJoinRequests
                    .Include(jr => jr.Group)
                    .Include(jr => jr.User)
                    .FirstOrDefaultAsync(jr => jr.Id == requestId);

                if (request == null)
                {
                    return (false, _localizer["JoinRequestNotFound"].Value);
                }

                // Check if approver is creator or admin
                var isCreator = request.Group.CreatorId == approverId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == request.GroupId && gu.UserId == approverId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsApprove"].Value);
                }

                // Add user as member (with rules agreement true since they made it through the join request)
                var addResult = await AddMemberAsync(request.GroupId, request.UserId, hasAgreedToRules: true);
                if (!addResult.Success)
                {
                    return addResult;
                }

                // Remove the join request
                context.GroupJoinRequests.Remove(request);
                await context.SaveChangesAsync();

                _logger.LogInformation("Join request {RequestId} approved by user {ApproverId}", requestId, approverId);

                // Log join request approval to audit log
                await _auditLogRepository.LogActionAsync(approverId, ActionCategory.GroupAdminApproveJoinRequest, new GroupAdminApproveJoinRequestDetails
                {
                    GroupId = request.GroupId,
                    GroupName = request.Group.Name,
                    JoinRequestId = requestId,
                    ApprovedUserId = request.UserId,
                    ApprovedUserDisplayName = request.User != null ? $"{request.User.FirstName} {request.User.LastName}" : null,
                    RequestedAt = request.RequestedAt
                });

                await LogModerationAsync(request.GroupId, approverId, GroupModerationAction.JoinRequestApproved, request.UserId);

                // Send notification to the user that their join request was approved
                await _notificationService.CreateNotificationAsync(
                    request.UserId,
                    NotificationType.GroupJoinApproved,
                    $"Your request to join '{request.Group.Name}' has been approved. You are now a member!",
                    new { GroupId = request.GroupId, GroupName = request.Group.Name }
                );

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving join request {RequestId} by user {ApproverId}", requestId, approverId);
                return (false, _localizer["ErrorApprovingJoinRequest"].Value);
            }
        }

        /// <summary>
        /// Reject a join request
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RejectJoinRequestAsync(
            int requestId,
            string rejecterId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var request = await context.GroupJoinRequests
                    .Include(jr => jr.Group)
                    .Include(jr => jr.User)
                    .FirstOrDefaultAsync(jr => jr.Id == requestId);

                if (request == null)
                {
                    return (false, _localizer["JoinRequestNotFound"].Value);
                }

                // Check if rejecter is creator or admin
                var isCreator = request.Group.CreatorId == rejecterId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == request.GroupId && gu.UserId == rejecterId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsReject"].Value);
                }

                // Capture details before removing
                var groupId = request.GroupId;
                var groupName = request.Group.Name;
                var deniedUserId = request.UserId;
                var deniedUserDisplayName = request.User != null ? $"{request.User.FirstName} {request.User.LastName}" : null;
                var requestedAt = request.RequestedAt;

                context.GroupJoinRequests.Remove(request);
                await context.SaveChangesAsync();

                _logger.LogInformation("Join request {RequestId} rejected by user {RejecterId}", requestId, rejecterId);

                // Log join request denial to audit log
                await _auditLogRepository.LogActionAsync(rejecterId, ActionCategory.GroupAdminDenyJoinRequest, new GroupAdminDenyJoinRequestDetails
                {
                    GroupId = groupId,
                    GroupName = groupName,
                    JoinRequestId = requestId,
                    DeniedUserId = deniedUserId,
                    DeniedUserDisplayName = deniedUserDisplayName,
                    RequestedAt = requestedAt
                });

                await LogModerationAsync(groupId, rejecterId, GroupModerationAction.JoinRequestDenied, deniedUserId);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting join request {RequestId} by user {RejecterId}", requestId, rejecterId);
                return (false, _localizer["ErrorRejectingJoinRequest"].Value);
            }
        }

        /// <summary>
        /// Cancel a join request (by the requester)
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> CancelJoinRequestAsync(
            int requestId,
            string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var request = await context.GroupJoinRequests
                    .Include(jr => jr.Group)
                    .FirstOrDefaultAsync(jr => jr.Id == requestId && jr.UserId == userId);

                if (request == null)
                {
                    return (false, _localizer["JoinRequestNotFound"].Value);
                }

                // Capture details before removing
                var groupId = request.GroupId;
                var groupName = request.Group?.Name ?? string.Empty;

                context.GroupJoinRequests.Remove(request);
                await context.SaveChangesAsync();

                // Log cancel join request to audit log
                await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserGroupRequests, new UserGroupRequestsDetails
                {
                    OperationType = OperationTypeEnum.RequestCancel.ToString(),
                    GroupId = groupId,
                    GroupName = groupName,
                    RequiresApproval = true
                });

                _logger.LogInformation("Join request {RequestId} cancelled by user {UserId}", requestId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling join request {RequestId} by user {UserId}", requestId, userId);
                return (false, _localizer["ErrorCancellingJoinRequest"].Value);
            }
        }

        #endregion

        #region Group Members

        /// <summary>
        /// Add a member to a group (internal method)
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user to add.</param>
        /// <param name="hasAgreedToRules">Whether the user has agreed to the group rules.</param>
        private async Task<(bool Success, string? ErrorMessage)> AddMemberAsync(int groupId, string userId, bool hasAgreedToRules = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if already a member
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
                if (isMember)
                {
                    return (false, _localizer["UserAlreadyMember"].Value);
                }

                // Check if user is banned from the group
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);
                if (isBanned)
                {
                    return (false, _localizer["UserBannedCannotAdd"].Value);
                }

                var groupUser = new GroupUser
                {
                    GroupId = groupId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    PostCount = 0,
                    GroupPoints = 0,
                    IsAdmin = false,
                    IsModerator = false,
                    HasAgreedToRules = hasAgreedToRules
                };

                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();

                await GroupMemberCountHelper.RecomputeAsync(context, groupId);

                _logger.LogInformation("User {UserId} added to group {GroupId} with rules agreement: {HasAgreedToRules}", userId, groupId, hasAgreedToRules);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, groupId);
                return (false, _localizer["ErrorAddingMember"].Value);
            }
        }

        /// <summary>
        /// Join a public group (no approval required)
        /// </summary>
        /// <param name="groupId">The ID of the group to join.</param>
        /// <param name="userId">The ID of the user joining.</param>
        /// <param name="hasAgreedToRules">Whether the user has agreed to the group rules (required if group has RequireAcceptRules set).</param>
        public async Task<(bool Success, string? ErrorMessage)> JoinGroupAsync(int groupId, string userId, bool hasAgreedToRules = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                if (!group.IsPublic)
                {
                    return (false, _localizer["NotPublicGroup"].Value);
                }

                if (group.RequiresJoinApproval)
                {
                    return (false, _localizer["RequiresApprovalToJoin"].Value);
                }

                // Check if group requires rules acceptance
                if (group.RequireAcceptRules && !hasAgreedToRules)
                {
                    return (false, _localizer["MustAgreeToRulesBeforeJoining"].Value);
                }

                // Check if user is banned from the group
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);
                if (isBanned)
                {
                    return (false, _localizer["BannedCannotJoin"].Value);
                }

                var result = await AddMemberAsync(groupId, userId, hasAgreedToRules);
                if (result.Success)
                {
                    // Log direct group join to audit log
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserGroupRequests, new UserGroupRequestsDetails
                    {
                        OperationType = OperationTypeEnum.DirectJoin.ToString(),
                        GroupId = groupId,
                        GroupName = group.Name,
                        RequiresApproval = false
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining group {GroupId} by user {UserId}", groupId, userId);
                return (false, _localizer["ErrorJoiningGroup"].Value);
            }
        }

        /// <summary>
        /// Get all members of a group
        /// </summary>
        public async Task<List<GroupUser>> GetGroupMembersAsync(
            int groupId,
            int pageSize = 50,
            int pageNumber = 1)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .Include(gu => gu.User)
                    .Where(gu => gu.GroupId == groupId)
                    .OrderByDescending(gu => gu.JoinedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members for group {GroupId}", groupId);
                return new List<GroupUser>();
            }
        }

        /// <summary>
        /// Get groups a user is a member of
        /// </summary>
        public async Task<List<GroupUser>> GetUserGroupsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .Include(gu => gu.Group)
                    .Where(gu => gu.UserId == userId)
                    .OrderByDescending(gu => gu.LastActiveAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups for user {UserId}", userId);
                return new List<GroupUser>();
            }
        }

        /// <summary>
        /// Check if a user is a member of a group
        /// </summary>
        public async Task<bool> IsMemberAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking membership for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Get member count for a group
        /// </summary>
        public async Task<int> GetMemberCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .CountAsync(gu => gu.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting member count for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the total number of groups a user is a member of.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The count of groups the user belongs to.</returns>
        public async Task<int> GetUserGroupCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupUsers
                    .CountAsync(gu => gu.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Check if a user is an admin of a group, or a system administrator.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is an admin or system administrator; otherwise, false.</returns>
        public async Task<bool> IsAdminAsync(int groupId, string userId)
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

                var membership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
                return membership?.IsAdmin == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin status for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Check if a user is a moderator of a group, or a system administrator.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is a moderator or system administrator; otherwise, false.</returns>
        public async Task<bool> IsModeratorAsync(int groupId, string userId)
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

                var membership = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
                return membership?.IsModerator == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking moderator status for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        #endregion

        #region Update Members

        /// <summary>
        /// Update member's agreement to rules
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AgreeToRulesAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var groupUser = await context.GroupUsers
                    .Include(gu => gu.Group)
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (groupUser == null)
                {
                    return (false, _localizer["NotMemberOfGroup"].Value);
                }

                var isFirstAgreement = !groupUser.HasAgreedToRules;
                groupUser.HasAgreedToRules = true;
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} agreed to rules for group {GroupId}", userId, groupId);

                // Get rule count for audit log
                var ruleCount = await context.GroupRules.CountAsync(r => r.GroupId == groupId);

                // Log agree to rules to audit log
                await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserGroupAgreeToRules, new UserGroupAgreeToRulesDetails
                {
                    GroupId = groupId,
                    GroupName = groupUser.Group?.Name ?? string.Empty,
                    RuleCount = ruleCount,
                    IsFirstAgreement = isFirstAgreement
                });

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rule agreement for user {UserId} in group {GroupId}", userId, groupId);
                return (false, _localizer["ErrorUpdatingRuleAgreement"].Value);
            }
        }

        /// <summary>
        /// Update member's last active timestamp
        /// </summary>
        public async Task<bool> UpdateLastActiveAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (groupUser != null)
                {
                    groupUser.LastActiveAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last active for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Increment member's post count
        /// </summary>
        public async Task<bool> IncrementPostCountAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (groupUser != null)
                {
                    groupUser.PostCount++;
                    groupUser.LastActiveAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing post count for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Update member's group points
        /// </summary>
        public async Task<bool> UpdateGroupPointsAsync(int groupId, string userId, int delta)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (groupUser != null)
                {
                    groupUser.GroupPoints += delta;
                    if (groupUser.GroupPoints < 0)
                    {
                        groupUser.GroupPoints = 0;
                    }
                    await context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group points for user {UserId} in group {GroupId}", userId, groupId);
                return false;
            }
        }

        /// <summary>
        /// Set member as admin
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SetAdminAsync(
            int groupId,
            string targetUserId,
            bool isAdmin,
            string requesterId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                // Check if requester is creator or system administrator
                var isCreator = group.CreatorId == requesterId;
                var isSysAdmin = await _roleService.IsSystemAdministratorAsync(requesterId);

                if (!isCreator && !isSysAdmin)
                {
                    return (false, _localizer["OnlyCreatorAssignAdmin"].Value);
                }

                var groupUser = await context.GroupUsers
                    .Include(gu => gu.User)
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == targetUserId);

                if (groupUser == null)
                {
                    return (false, _localizer["UserNotMember"].Value);
                }

                // Determine old role for audit
                var oldRole = groupUser.IsAdmin ? "Admin" : (groupUser.IsModerator ? "Moderator" : "Member");

                groupUser.IsAdmin = isAdmin;
                await context.SaveChangesAsync();

                // Determine new role for audit
                var newRole = isAdmin ? "Admin" : (groupUser.IsModerator ? "Moderator" : "Member");

                _logger.LogInformation("User {TargetUserId} admin status set to {IsAdmin} in group {GroupId} by {RequesterId}", 
                    targetUserId, isAdmin, groupId, requesterId);

                // Log role change to audit log
                await _auditLogRepository.LogActionAsync(requesterId, ActionCategory.GroupAdminChangeMemberRole, new GroupAdminChangeMemberRoleDetails
                {
                    GroupId = groupId,
                    GroupName = group.Name,
                    TargetUserId = targetUserId,
                    TargetUserDisplayName = groupUser.User != null ? $"{groupUser.User.FirstName} {groupUser.User.LastName}" : null,
                    OldRole = oldRole,
                    NewRole = newRole
                });

                if (oldRole != newRole)
                {
                    await LogModerationAsync(groupId, requesterId, GroupModerationAction.MemberRoleChanged, targetUserId, $"{oldRole} -> {newRole}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting admin status for user {TargetUserId} in group {GroupId}", targetUserId, groupId);
                return (false, _localizer["ErrorUpdatingAdminStatus"].Value);
            }
        }

        /// <summary>
        /// Set member as moderator
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SetModeratorAsync(
            int groupId,
            string targetUserId,
            bool isModerator,
            string requesterId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                // Check if requester is creator, admin, or system administrator
                var isCreator = group.CreatorId == requesterId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == requesterId && gu.IsAdmin);
                var isSysAdmin = await _roleService.IsSystemAdministratorAsync(requesterId);

                if (!isCreator && !isAdmin && !isSysAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsAssignModerator"].Value);
                }

                var groupUser = await context.GroupUsers
                    .Include(gu => gu.User)
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == targetUserId);

                if (groupUser == null)
                {
                    return (false, _localizer["UserNotMember"].Value);
                }

                // Determine old role for audit
                var oldRole = groupUser.IsAdmin ? "Admin" : (groupUser.IsModerator ? "Moderator" : "Member");

                groupUser.IsModerator = isModerator;
                await context.SaveChangesAsync();

                // Determine new role for audit
                var newRole = groupUser.IsAdmin ? "Admin" : (isModerator ? "Moderator" : "Member");

                _logger.LogInformation("User {TargetUserId} moderator status set to {IsModerator} in group {GroupId} by {RequesterId}", 
                    targetUserId, isModerator, groupId, requesterId);

                // Log role change to audit log
                await _auditLogRepository.LogActionAsync(requesterId, ActionCategory.GroupAdminChangeMemberRole, new GroupAdminChangeMemberRoleDetails
                {
                    GroupId = groupId,
                    GroupName = group.Name,
                    TargetUserId = targetUserId,
                    TargetUserDisplayName = groupUser.User != null ? $"{groupUser.User.FirstName} {groupUser.User.LastName}" : null,
                    OldRole = oldRole,
                    NewRole = newRole
                });

                if (oldRole != newRole)
                {
                    await LogModerationAsync(groupId, requesterId, GroupModerationAction.MemberRoleChanged, targetUserId, $"{oldRole} -> {newRole}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting moderator status for user {TargetUserId} in group {GroupId}", targetUserId, groupId);
                return (false, _localizer["ErrorUpdatingModeratorStatus"].Value);
            }
        }

        #endregion

        #region Remove Members

        /// <summary>
        /// Leave a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> LeaveGroupAsync(int groupId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                // Prevent creator from leaving their own group
                if (group.CreatorId == userId)
                {
                    return (false, _localizer["CreatorCannotLeave"].Value);
                }

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

                if (groupUser == null)
                {
                    return (false, _localizer["NotMemberOfGroup"].Value);
                }

                context.GroupUsers.Remove(groupUser);
                await context.SaveChangesAsync();

                await GroupMemberCountHelper.RecomputeAsync(context, groupId);

                                // Log group leave to audit log
                                await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserGroupLeave, new UserGroupLeaveDetails
                                {
                                    LeaveType = "Voluntary",
                                    GroupId = groupId,
                                    GroupName = group.Name
                                });

                                _logger.LogInformation("User {UserId} left group {GroupId}", userId, groupId);
                                return (true, null);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error leaving group {GroupId} by user {UserId}", groupId, userId);
                                return (false, _localizer["ErrorLeavingGroup"].Value);
                            }
                        }

                        /// <summary>
                        /// Remove a member from a group (by admin/creator)
                        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RemoveMemberAsync(
            int groupId,
            string targetUserId,
            string requesterId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                // Check if requester is system administrator
                var isSystemAdmin = await _roleService.IsSystemAdministratorAsync(requesterId);

                // Check if requester is creator or admin
                var isCreator = group.CreatorId == requesterId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == requesterId && gu.IsAdmin);

                if (!isSystemAdmin && !isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsRemoveMembers"].Value);
                }

                // Cannot remove the creator
                if (group.CreatorId == targetUserId)
                {
                    return (false, _localizer["CannotRemoveCreator"].Value);
                }

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == targetUserId);

                if (groupUser == null)
                {
                    return (false, _localizer["UserNotMember"].Value);
                }

                // Only the creator or a system admin can remove another admin
                if (groupUser.IsAdmin && !isSystemAdmin && !isCreator)
                {
                    return (false, _localizer["OnlyCreatorRemoveAdmins"].Value);
                }

                context.GroupUsers.Remove(groupUser);
                await context.SaveChangesAsync();

                await GroupMemberCountHelper.RecomputeAsync(context, groupId);

                // Log member removal to audit log (logged from removed user's perspective)
                await _auditLogRepository.LogActionAsync(targetUserId, ActionCategory.UserGroupLeave, new UserGroupLeaveDetails
                {
                    LeaveType = "RemovedByAdmin",
                    GroupId = groupId,
                    GroupName = group.Name
                });

                await LogModerationAsync(groupId, requesterId, GroupModerationAction.MemberRemoved, targetUserId);

                _logger.LogInformation("User {TargetUserId} removed from group {GroupId} by {RequesterId}", 
                    targetUserId, groupId, requesterId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {TargetUserId} from group {GroupId} by {RequesterId}", 
                    targetUserId, groupId, requesterId);
                return (false, _localizer["ErrorRemovingMember"].Value);
            }
        }

        #endregion
    }
}
