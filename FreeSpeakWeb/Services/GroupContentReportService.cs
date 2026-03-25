using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing content reports in groups.
    /// Handles creation and retrieval of reports for posts and comments.
    /// </summary>
    public class GroupContentReportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupContentReportService> _logger;
        private readonly GroupAccessValidator _accessValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupContentReportService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="accessValidator">Validator for group access permissions.</param>
        public GroupContentReportService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupContentReportService> logger,
            GroupAccessValidator accessValidator)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _accessValidator = accessValidator;
        }

        #region Create Reports

        /// <summary>
        /// Creates a new report for a group post.
        /// </summary>
        /// <param name="groupId">The ID of the group containing the post.</param>
        /// <param name="postId">The ID of the post being reported.</param>
        /// <param name="reporterId">The ID of the user submitting the report.</param>
        /// <param name="reason">The reason category for the report.</param>
        /// <param name="violatedRuleId">The ID of the violated rule, if reason is ViolatesGroupRule.</param>
        /// <param name="description">Additional description provided by the reporter.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created report if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, GroupContentReport? Report)> ReportPostAsync(
            int groupId,
            int postId,
            string reporterId,
            ReportReason reason,
            int? violatedRuleId = null,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(reporterId))
            {
                return (false, "Reporter ID is required.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify group exists
                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, "Group not found.", null);
                }

                // Verify post exists and belongs to the group
                var post = await context.GroupPosts.FirstOrDefaultAsync(p => p.Id == postId && p.GroupId == groupId);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                // Check if user is a member of the group
                var isMember = await context.GroupUsers.AnyAsync(gu => gu.GroupId == groupId && gu.UserId == reporterId);
                if (!isMember)
                {
                    return (false, "You must be a member of the group to report content.", null);
                }

                // Check if user has already reported this post
                var existingReport = await context.GroupContentReports
                    .AnyAsync(r => r.GroupId == groupId && r.PostId == postId && r.ReporterId == reporterId);
                if (existingReport)
                {
                    return (false, "You have already reported this post.", null);
                }

                // Validate violated rule if reason is ViolatesGroupRule
                if (reason == ReportReason.ViolatesGroupRule)
                {
                    if (!violatedRuleId.HasValue)
                    {
                        return (false, "Please select which group rule was violated.", null);
                    }

                    var ruleExists = await context.GroupRules.AnyAsync(r => r.Id == violatedRuleId.Value && r.GroupId == groupId);
                    if (!ruleExists)
                    {
                        return (false, "The selected rule does not exist.", null);
                    }
                }

                var report = new GroupContentReport
                {
                    GroupId = groupId,
                    PostId = postId,
                    CommentId = null,
                    ReporterId = reporterId,
                    Reason = reason,
                    ViolatedRuleId = reason == ReportReason.ViolatesGroupRule ? violatedRuleId : null,
                    Description = description?.Trim(),
                    Status = ReportStatus.NotReviewed,
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupContentReports.Add(report);
                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} reported by user {ReporterId} in group {GroupId} for reason {Reason}",
                    postId, reporterId, groupId, reason);

                return (true, null, report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting post {PostId} by user {ReporterId}", postId, reporterId);
                return (false, "An error occurred while submitting the report.", null);
            }
        }

        /// <summary>
        /// Creates a new report for a group post comment.
        /// </summary>
        /// <param name="groupId">The ID of the group containing the comment.</param>
        /// <param name="commentId">The ID of the comment being reported.</param>
        /// <param name="reporterId">The ID of the user submitting the report.</param>
        /// <param name="reason">The reason category for the report.</param>
        /// <param name="violatedRuleId">The ID of the violated rule, if reason is ViolatesGroupRule.</param>
        /// <param name="description">Additional description provided by the reporter.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created report if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, GroupContentReport? Report)> ReportCommentAsync(
            int groupId,
            int commentId,
            string reporterId,
            ReportReason reason,
            int? violatedRuleId = null,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(reporterId))
            {
                return (false, "Reporter ID is required.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify group exists
                var group = await context.Groups.FindAsync(groupId);
                if (group == null)
                {
                    return (false, "Group not found.", null);
                }

                // Verify comment exists and belongs to a post in the group
                var comment = await context.GroupPostComments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId && c.Post.GroupId == groupId);
                if (comment == null)
                {
                    return (false, "Comment not found.", null);
                }

                // Check if user is a member of the group
                var isMember = await context.GroupUsers.AnyAsync(gu => gu.GroupId == groupId && gu.UserId == reporterId);
                if (!isMember)
                {
                    return (false, "You must be a member of the group to report content.", null);
                }

                // Check if user has already reported this comment
                var existingReport = await context.GroupContentReports
                    .AnyAsync(r => r.GroupId == groupId && r.CommentId == commentId && r.ReporterId == reporterId);
                if (existingReport)
                {
                    return (false, "You have already reported this comment.", null);
                }

                // Validate violated rule if reason is ViolatesGroupRule
                if (reason == ReportReason.ViolatesGroupRule)
                {
                    if (!violatedRuleId.HasValue)
                    {
                        return (false, "Please select which group rule was violated.", null);
                    }

                    var ruleExists = await context.GroupRules.AnyAsync(r => r.Id == violatedRuleId.Value && r.GroupId == groupId);
                    if (!ruleExists)
                    {
                        return (false, "The selected rule does not exist.", null);
                    }
                }

                var report = new GroupContentReport
                {
                    GroupId = groupId,
                    PostId = null,
                    CommentId = commentId,
                    ReporterId = reporterId,
                    Reason = reason,
                    ViolatedRuleId = reason == ReportReason.ViolatesGroupRule ? violatedRuleId : null,
                    Description = description?.Trim(),
                    Status = ReportStatus.NotReviewed,
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupContentReports.Add(report);
                await context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} reported by user {ReporterId} in group {GroupId} for reason {Reason}",
                    commentId, reporterId, groupId, reason);

                return (true, null, report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting comment {CommentId} by user {ReporterId}", commentId, reporterId);
                return (false, "An error occurred while submitting the report.", null);
            }
        }

        #endregion

        #region Retrieve Reports

        /// <summary>
        /// Gets all pending (not reviewed) reports for a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>A list of pending reports.</returns>
        public async Task<List<GroupContentReport>> GetPendingReportsAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupContentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Post)
                    .Include(r => r.Comment)
                    .Include(r => r.ViolatedRule)
                    .Where(r => r.GroupId == groupId && r.Status == ReportStatus.NotReviewed)
                    .OrderBy(r => r.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending reports for group {GroupId}", groupId);
                return new List<GroupContentReport>();
            }
        }

        /// <summary>
        /// Gets the count of pending reports for a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The number of pending reports.</returns>
        public async Task<int> GetPendingReportCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupContentReports
                    .CountAsync(r => r.GroupId == groupId && r.Status == ReportStatus.NotReviewed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting pending reports for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the IDs of posts that have pending (not reviewed) reports in a group.
        /// Used to display a "reported" indicator on posts in the group feed.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>A HashSet of post IDs that have pending reports.</returns>
        public async Task<HashSet<int>> GetPostIdsWithPendingReportsAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var postIds = await context.GroupContentReports
                    .Where(r => r.GroupId == groupId && r.Status == ReportStatus.NotReviewed && r.PostId.HasValue)
                    .Select(r => r.PostId!.Value)
                    .Distinct()
                    .ToListAsync();

                return postIds.ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post IDs with pending reports for group {GroupId}", groupId);
                return new HashSet<int>();
            }
        }

        /// <summary>
        /// Gets the IDs of posts that have pending (not reviewed) reports from a list of post IDs.
        /// Used to display a "reported" indicator on posts across multiple groups (e.g., My Feed).
        /// </summary>
        /// <param name="postIds">The list of post IDs to check.</param>
        /// <returns>A HashSet of post IDs that have pending reports.</returns>
        public async Task<HashSet<int>> GetPostIdsWithPendingReportsFromListAsync(IEnumerable<int> postIds)
        {
            if (postIds == null || !postIds.Any())
            {
                return new HashSet<int>();
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var reportedPostIds = await context.GroupContentReports
                    .Where(r => r.PostId.HasValue && postIds.Contains(r.PostId.Value) && r.Status == ReportStatus.NotReviewed)
                    .Select(r => r.PostId!.Value)
                    .Distinct()
                    .ToListAsync();

                return reportedPostIds.ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reported post IDs from list");
                return new HashSet<int>();
            }
        }

        /// <summary>
        /// Checks if a specific post has any pending reports.
        /// </summary>
        /// <param name="postId">The ID of the post to check.</param>
        /// <returns>True if the post has pending reports; otherwise, false.</returns>
        public async Task<bool> HasPendingReportAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupContentReports
                    .AnyAsync(r => r.PostId == postId && r.Status == ReportStatus.NotReviewed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking pending reports for post {PostId}", postId);
                return false;
            }
        }

        /// <summary>
        /// Gets all reports for a group with optional status filter.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="status">Optional status filter.</param>
        /// <param name="skip">Number of records to skip for pagination.</param>
        /// <param name="take">Number of records to take for pagination.</param>
        /// <returns>A list of reports matching the criteria.</returns>
        public async Task<List<GroupContentReport>> GetReportsAsync(
            int groupId,
            ReportStatus? status = null,
            int skip = 0,
            int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.GroupContentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Post)
                    .Include(r => r.Comment)
                    .Include(r => r.ViolatedRule)
                    .Include(r => r.Reviewer)
                    .Where(r => r.GroupId == groupId);

                if (status.HasValue)
                {
                    query = query.Where(r => r.Status == status.Value);
                }

                return await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports for group {GroupId}", groupId);
                return new List<GroupContentReport>();
            }
        }

        #endregion

        #region Review Reports

        /// <summary>
        /// Updates the status of a report after review.
        /// </summary>
        /// <param name="reportId">The ID of the report to update.</param>
        /// <param name="reviewerId">The ID of the user reviewing the report.</param>
        /// <param name="newStatus">The new status to set.</param>
        /// <param name="reviewerNotes">Optional notes from the reviewer.</param>
        /// <returns>A tuple containing success status and an error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdateReportStatusAsync(
            int reportId,
            string reviewerId,
            ReportStatus newStatus,
            string? reviewerNotes = null)
        {
            if (string.IsNullOrWhiteSpace(reviewerId))
            {
                return (false, "Reviewer ID is required.");
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var report = await context.GroupContentReports.FindAsync(reportId);
                if (report == null)
                {
                    return (false, "Report not found.");
                }

                // Check if user is a group admin, moderator, or system administrator
                var isAuthorized = await _accessValidator.IsGroupAdminOrModeratorAsync(report.GroupId, reviewerId);
                if (!isAuthorized)
                {
                    return (false, "You are not authorized to review reports in this group.");
                }

                report.Status = newStatus;
                report.ReviewerId = reviewerId;
                report.ReviewedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(reviewerNotes))
                {
                    report.ReviewerNotes = reviewerNotes.Trim();
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Report {ReportId} status updated to {Status} by reviewer {ReviewerId}",
                    reportId, newStatus, reviewerId);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report {ReportId} status", reportId);
                return (false, "An error occurred while updating the report.");
            }
        }

        /// <summary>
        /// Dismisses a report as not violating any rules.
        /// </summary>
        /// <param name="reportId">The ID of the report to dismiss.</param>
        /// <param name="reviewerId">The ID of the user dismissing the report.</param>
        /// <param name="reviewerNotes">Optional notes explaining the dismissal.</param>
        /// <returns>A tuple containing success status and an error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> DismissReportAsync(
            int reportId,
            string reviewerId,
            string? reviewerNotes = null)
        {
            return await UpdateReportStatusAsync(reportId, reviewerId, ReportStatus.Dismissed, reviewerNotes);
        }

        /// <summary>
        /// Marks a report as having action taken against the content.
        /// </summary>
        /// <param name="reportId">The ID of the report.</param>
        /// <param name="reviewerId">The ID of the user taking action.</param>
        /// <param name="reviewerNotes">Optional notes explaining the action taken.</param>
        /// <returns>A tuple containing success status and an error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> MarkActionTakenAsync(
            int reportId,
            string reviewerId,
            string? reviewerNotes = null)
        {
            return await UpdateReportStatusAsync(reportId, reviewerId, ReportStatus.ActionTaken, reviewerNotes);
        }

        /// <summary>
        /// Marks a report as resolved without requiring action.
        /// </summary>
        /// <param name="reportId">The ID of the report.</param>
        /// <param name="reviewerId">The ID of the user resolving the report.</param>
        /// <param name="reviewerNotes">Optional notes explaining the resolution.</param>
        /// <returns>A tuple containing success status and an error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> ResolveReportAsync(
            int reportId,
            string reviewerId,
            string? reviewerNotes = null)
        {
            return await UpdateReportStatusAsync(reportId, reviewerId, ReportStatus.Resolved, reviewerNotes);
        }

        /// <summary>
        /// Gets a single report by ID with all related data.
        /// </summary>
        /// <param name="reportId">The ID of the report.</param>
        /// <returns>The report if found, otherwise null.</returns>
        public async Task<GroupContentReport?> GetReportByIdAsync(int reportId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupContentReports
                    .Include(r => r.Reporter)
                    .Include(r => r.Post)
                        .ThenInclude(p => p!.Author)
                    .Include(r => r.Comment)
                        .ThenInclude(c => c!.Author)
                    .Include(r => r.ViolatedRule)
                    .Include(r => r.Reviewer)
                    .FirstOrDefaultAsync(r => r.Id == reportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report {ReportId}", reportId);
                return null;
            }
        }

        #endregion
    }
}
