using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class GroupRuleService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupRuleService> _logger;

        public GroupRuleService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupRuleService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
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
                return (false, "Rule title is required.", null);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return (false, "Rule description is required.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify group exists and user has permission
                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, "Group not found.", null);
                }

                // Check if user is creator or admin
                var isCreator = group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, "Only group creator or admins can create rules.", null);
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

                _logger.LogInformation("Rule {RuleId} created for group {GroupId} by user {UserId}", 
                    rule.Id, groupId, userId);

                return (true, null, rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rule for group {GroupId} by user {UserId}", groupId, userId);
                return (false, "An error occurred while creating the rule.", null);
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
                    return (false, "Rule not found.");
                }

                // Check if user is creator or admin
                var isCreator = rule.Group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == rule.GroupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, "Only group creator or admins can update rules.");
                }

                // Update fields if provided
                if (!string.IsNullOrWhiteSpace(title))
                {
                    rule.Title = title;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    rule.Description = description;
                }

                if (order.HasValue)
                {
                    rule.Order = order.Value;
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Rule {RuleId} updated by user {UserId}", ruleId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rule {RuleId} by user {UserId}", ruleId, userId);
                return (false, "An error occurred while updating the rule.");
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
                    return (false, "Group not found.");
                }

                // Check if user is creator or admin
                var isCreator = group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, "Only group creator or admins can reorder rules.");
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
                return (false, "An error occurred while reordering rules.");
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
                    return (false, "Rule not found.");
                }

                // Check if user is creator or admin
                var isCreator = rule.Group.CreatorId == userId;
                var isAdmin = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == rule.GroupId && gu.UserId == userId && gu.IsAdmin);

                if (!isCreator && !isAdmin)
                {
                    return (false, "Only group creator or admins can delete rules.");
                }

                context.GroupRules.Remove(rule);
                await context.SaveChangesAsync();

                _logger.LogInformation("Rule {RuleId} deleted by user {UserId}", ruleId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rule {RuleId} by user {UserId}", ruleId, userId);
                return (false, "An error occurred while deleting the rule.");
            }
        }

        #endregion
    }
}
