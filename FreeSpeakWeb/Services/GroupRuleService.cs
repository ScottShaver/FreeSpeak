using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing group rules. Rule changes are also recorded in the group moderation log.
    /// </summary>
    public class GroupRuleService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupRuleService> _logger;
        private readonly IGroupModerationLogService? _moderationLog;
        private readonly IStringLocalizer<GroupRuleService> _localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupRuleService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="moderationLog">Optional per-group moderation log; when omitted nothing is recorded there.</param>
        /// <param name="localizer">Optional localizer for user-facing result messages; when omitted the neutral (English) text is used.</param>
        public GroupRuleService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupRuleService> logger,
            IGroupModerationLogService? moderationLog = null,
            IStringLocalizer<GroupRuleService>? localizer = null)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _moderationLog = moderationLog;
            _localizer = ServiceLocalizer.Resolve(localizer);
        }

        /// <summary>
        /// Records a rule change in the group moderation log. Logging failures never affect the caller.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="actorUserId">The admin who changed the rule.</param>
        /// <param name="action">The rule action taken.</param>
        /// <param name="ruleId">The rule id.</param>
        /// <param name="ruleTitle">The rule title, used as the entry summary.</param>
        private async Task LogModerationAsync(int groupId, string actorUserId, GroupModerationAction action, int ruleId, string ruleTitle)
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
                TargetType = GroupModerationTargetType.Group,
                TargetId = ruleId.ToString(),
                Summary = ruleTitle
            });
        }

        #region Create Rules

        /// <summary>
        /// Create a new rule for a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, GroupRule? Rule)> CreateRuleAsync(
            int groupId,
            string userId,
            string title,
            string description,
            int? order = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, _localizer["RuleTitleRequired"].Value, null);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return (false, _localizer["RuleDescriptionRequired"].Value, null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify group exists and user has permission
                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value, null);
                }

                // Check if user is creator or admin
                var isCreator = group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsCreateRules"].Value, null);
                }

                // If no order specified, put it at the end
                if (!order.HasValue)
                {
                    var maxOrder = await context.GroupRules
                        .Where(r => r.GroupId == groupId)
                        .MaxAsync(r => (int?)r.Order) ?? 0;
                    order = maxOrder + 1;
                }

                var rule = new GroupRule
                {
                    GroupId = groupId,
                    Title = title,
                    Description = description,
                    Order = order.Value
                };

                context.GroupRules.Add(rule);
                await context.SaveChangesAsync();

                await LogModerationAsync(groupId, userId, GroupModerationAction.RuleAdded, rule.Id, rule.Title);

                _logger.LogInformation("Rule {RuleId} created for group {GroupId} by user {UserId}", 
                    rule.Id, groupId, userId);

                return (true, null, rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rule for group {GroupId} by user {UserId}", groupId, userId);
                return (false, _localizer["ErrorCreatingRule"].Value, null);
            }
        }

        #endregion

        #region Retrieve Rules

        /// <summary>
        /// Get all rules for a group
        /// </summary>
        public async Task<List<GroupRule>> GetGroupRulesAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupRules
                    .Where(r => r.GroupId == groupId)
                    .OrderBy(r => r.Order)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rules for group {GroupId}", groupId);
                return new List<GroupRule>();
            }
        }

        /// <summary>
        /// Get a specific rule by ID
        /// </summary>
        public async Task<GroupRule?> GetRuleByIdAsync(int ruleId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupRules.FindAsync(ruleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rule {RuleId}", ruleId);
                return null;
            }
        }

        #endregion

        #region Update Rules

        /// <summary>
        /// Update a rule
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UpdateRuleAsync(
            int ruleId,
            string userId,
            string? title = null,
            string? description = null,
            int? order = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var rule = await context.GroupRules
                    .Include(r => r.Group)
                    .FirstOrDefaultAsync(r => r.Id == ruleId);

                if (rule == null)
                {
                    return (false, _localizer["RuleNotFound"].Value);
                }

                // Check if user is creator or admin
                var isCreator = rule.Group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == rule.GroupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsUpdateRules"].Value);
                }

                // Update fields if provided
                var changed = false;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    changed |= rule.Title != title;
                    rule.Title = title;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    changed |= rule.Description != description;
                    rule.Description = description;
                }

                if (order.HasValue)
                {
                    changed |= rule.Order != order.Value;
                    rule.Order = order.Value;
                }

                await context.SaveChangesAsync();

                if (changed)
                {
                    await LogModerationAsync(rule.GroupId, userId, GroupModerationAction.RuleEdited, rule.Id, rule.Title);
                }

                _logger.LogInformation("Rule {RuleId} updated by user {UserId}", ruleId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rule {RuleId} by user {UserId}", ruleId, userId);
                return (false, _localizer["ErrorUpdatingRule"].Value);
            }
        }

        /// <summary>
        /// Reorder rules for a group
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ReorderRulesAsync(
            int groupId,
            string userId,
            List<int> ruleIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, _localizer["GroupNotFound"].Value);
                }

                // Check if user is creator or admin
                var isCreator = group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsReorderRules"].Value);
                }

                var rules = await context.GroupRules
                    .Where(r => r.GroupId == groupId && ruleIds.Contains(r.Id))
                    .ToListAsync();

                for (int i = 0; i < ruleIds.Count; i++)
                {
                    var rule = rules.FirstOrDefault(r => r.Id == ruleIds[i]);
                    if (rule != null)
                    {
                        rule.Order = i;
                    }
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Rules reordered for group {GroupId} by user {UserId}", groupId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering rules for group {GroupId} by user {UserId}", groupId, userId);
                return (false, _localizer["ErrorReorderingRules"].Value);
            }
        }

        #endregion

        #region Delete Rules

        /// <summary>
        /// Delete a rule
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> DeleteRuleAsync(int ruleId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var rule = await context.GroupRules
                    .Include(r => r.Group)
                    .FirstOrDefaultAsync(r => r.Id == ruleId);

                if (rule == null)
                {
                    return (false, _localizer["RuleNotFound"].Value);
                }

                // Check if user is creator or admin
                var isCreator = rule.Group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == rule.GroupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, _localizer["OnlyCreatorOrAdminsDeleteRules"].Value);
                }

                context.GroupRules.Remove(rule);
                await context.SaveChangesAsync();

                await LogModerationAsync(rule.GroupId, userId, GroupModerationAction.RuleDeleted, rule.Id, rule.Title);

                _logger.LogInformation("Rule {RuleId} deleted by user {UserId}", ruleId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rule {RuleId} by user {UserId}", ruleId, userId);
                return (false, _localizer["ErrorDeletingRule"].Value);
            }
        }

        #endregion
    }
}
