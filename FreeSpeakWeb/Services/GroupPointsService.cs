using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing group member points and achievement levels.
    /// Handles point calculation, awarding, and member level determination based on accumulated points.
    /// </summary>
    public class GroupPointsService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupPointsService> _logger;

        // Point values for different actions
        public const int PointsForPostCreation = 20;
        public const int PointsForCommentOnOtherPost = 5;
        public const int PointsForLikeOnOtherComment = 1;
        public const int PointsForPost50Comments = 50;
        public const int PointsForPost20Likes = 30;

        // Member level thresholds
        public const int RisingContributorThreshold = 700;
        public const int TopContributorThreshold = 1500;
        public const int AllStarContributorThreshold = 5000;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupPointsService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        public GroupPointsService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupPointsService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Awards points to a group member for a specific action.
        /// Points are only awarded if the group has the points system enabled.
        /// </summary>
        /// <param name="userId">The user ID receiving the points.</param>
        /// <param name="groupId">The group ID where the action occurred.</param>
        /// <param name="points">The number of points to award (can be negative to deduct).</param>
        /// <returns>A tuple containing success status and the new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardPointsAsync(string userId, int groupId, int points)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("AwardPointsAsync called with null or empty userId");
                return (false, 0);
            }

            if (points == 0)
            {
                _logger.LogDebug("AwardPointsAsync called with 0 points for user {UserId} in group {GroupId}", userId, groupId);
                return (true, 0);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if the group has the points system enabled
                var group = await context.Groups
                    .Where(g => g.Id == groupId)
                    .Select(g => new { g.EnablePointsSystem })
                    .FirstOrDefaultAsync();

                if (group == null)
                {
                    _logger.LogWarning("Group {GroupId} not found", groupId);
                    return (false, 0);
                }

                if (!group.EnablePointsSystem)
                {
                    _logger.LogDebug("Points system is disabled for group {GroupId}, no points awarded", groupId);
                    return (true, 0);
                }

                var groupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.UserId == userId && gu.GroupId == groupId);

                if (groupUser == null)
                {
                    _logger.LogWarning("GroupUser record not found for user {UserId} in group {GroupId}", userId, groupId);
                    return (false, 0);
                }

                groupUser.GroupPoints += points;

                // Ensure points don't go below zero
                if (groupUser.GroupPoints < 0)
                {
                    groupUser.GroupPoints = 0;
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Awarded {Points} points to user {UserId} in group {GroupId}. New total: {NewTotal}",
                    points, userId, groupId, groupUser.GroupPoints);

                return (true, groupUser.GroupPoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error awarding points to user {UserId} in group {GroupId}", userId, groupId);
                return (false, 0);
            }
        }

        /// <summary>
        /// Gets the current point total for a user in a specific group.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="groupId">The group ID.</param>
        /// <returns>The current point total, or 0 if the user is not a member.</returns>
        public async Task<int> GetUserPointsAsync(string userId, int groupId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var groupUser = await context.GroupUsers
                    .Where(gu => gu.UserId == userId && gu.GroupId == groupId)
                    .Select(gu => gu.GroupPoints)
                    .FirstOrDefaultAsync();

                return groupUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting points for user {UserId} in group {GroupId}", userId, groupId);
                return 0;
            }
        }

        /// <summary>
        /// Determines the member level based on the current point total.
        /// </summary>
        /// <param name="points">The total number of points.</param>
        /// <returns>A <see cref="MemberLevel"/> representing the achievement level.</returns>
        public static MemberLevel GetMemberLevel(int points)
        {
            if (points >= AllStarContributorThreshold)
            {
                return MemberLevel.AllStarContributor;
            }
            else if (points >= TopContributorThreshold)
            {
                return MemberLevel.TopContributor;
            }
            else if (points >= RisingContributorThreshold)
            {
                return MemberLevel.RisingContributor;
            }

            return MemberLevel.Member;
        }

        /// <summary>
        /// Gets a display-friendly name for the member level.
        /// </summary>
        /// <param name="level">The member level.</param>
        /// <returns>A string representation of the level.</returns>
        public static string GetMemberLevelName(MemberLevel level)
        {
            return level switch
            {
                MemberLevel.AllStarContributor => "All-star Contributor",
                MemberLevel.TopContributor => "Top Contributor",
                MemberLevel.RisingContributor => "Rising Contributor",
                MemberLevel.Member => "Member",
                _ => "Member"
            };
        }

        /// <summary>
        /// Gets the icon CSS class for a member level badge.
        /// </summary>
        /// <param name="level">The member level.</param>
        /// <returns>A Bootstrap icon class name.</returns>
        public static string GetMemberLevelIcon(MemberLevel level)
        {
            return level switch
            {
                MemberLevel.AllStarContributor => "bi-star-fill",
                MemberLevel.TopContributor => "bi-award-fill",
                MemberLevel.RisingContributor => "bi-arrow-up-circle-fill",
                MemberLevel.Member => "bi-person-fill",
                _ => "bi-person-fill"
            };
        }

        /// <summary>
        /// Gets the CSS color class for a member level badge.
        /// </summary>
        /// <param name="level">The member level.</param>
        /// <returns>A CSS color class name.</returns>
        public static string GetMemberLevelColor(MemberLevel level)
        {
            return level switch
            {
                MemberLevel.AllStarContributor => "text-warning", // Gold
                MemberLevel.TopContributor => "text-primary", // Blue
                MemberLevel.RisingContributor => "text-success", // Green
                MemberLevel.Member => "text-secondary", // Gray
                _ => "text-secondary"
            };
        }

        /// <summary>
        /// Awards points for creating a post in a group.
        /// </summary>
        /// <param name="userId">The user who created the post.</param>
        /// <param name="groupId">The group where the post was created.</param>
        /// <returns>Success status and new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardPostCreationPointsAsync(string userId, int groupId)
        {
            return await AwardPointsAsync(userId, groupId, PointsForPostCreation);
        }

        /// <summary>
        /// Awards points for commenting on another user's post.
        /// </summary>
        /// <param name="commenterId">The user who made the comment.</param>
        /// <param name="postAuthorId">The author of the post being commented on.</param>
        /// <param name="groupId">The group where the comment was made.</param>
        /// <returns>Success status and new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardCommentPointsAsync(string commenterId, string postAuthorId, int groupId)
        {
            // Only award points if commenting on another user's post
            if (commenterId == postAuthorId)
            {
                _logger.LogDebug("User {UserId} commented on their own post in group {GroupId}, no points awarded", commenterId, groupId);
                return (true, 0);
            }

            return await AwardPointsAsync(commenterId, groupId, PointsForCommentOnOtherPost);
        }

        /// <summary>
        /// Awards points for liking another user's comment.
        /// </summary>
        /// <param name="likerId">The user who liked the comment.</param>
        /// <param name="commentAuthorId">The author of the comment being liked.</param>
        /// <param name="groupId">The group where the like occurred.</param>
        /// <returns>Success status and new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardLikePointsAsync(string likerId, string commentAuthorId, int groupId)
        {
            // Only award points if liking another user's comment
            if (likerId == commentAuthorId)
            {
                _logger.LogDebug("User {UserId} liked their own comment in group {GroupId}, no points awarded", likerId, groupId);
                return (true, 0);
            }

            return await AwardPointsAsync(likerId, groupId, PointsForLikeOnOtherComment);
        }

        /// <summary>
        /// Awards milestone points when a post reaches 50 comments.
        /// </summary>
        /// <param name="postAuthorId">The author of the post.</param>
        /// <param name="groupId">The group where the post is located.</param>
        /// <returns>Success status and new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardPost50CommentsMilestoneAsync(string postAuthorId, int groupId)
        {
            return await AwardPointsAsync(postAuthorId, groupId, PointsForPost50Comments);
        }

        /// <summary>
        /// Awards milestone points when a post reaches 20 likes.
        /// </summary>
        /// <param name="postAuthorId">The author of the post.</param>
        /// <param name="groupId">The group where the post is located.</param>
        /// <returns>Success status and new point total.</returns>
        public async Task<(bool Success, int NewTotal)> AwardPost20LikesMilestoneAsync(string postAuthorId, int groupId)
        {
            return await AwardPointsAsync(postAuthorId, groupId, PointsForPost20Likes);
        }
    }

    /// <summary>
    /// Represents the achievement levels for group members based on their accumulated points.
    /// </summary>
    public enum MemberLevel
    {
        /// <summary>
        /// Standard member with no special achievement level.
        /// </summary>
        Member = 0,

        /// <summary>
        /// Rising Contributor - achieved at 700 points.
        /// </summary>
        RisingContributor = 1,

        /// <summary>
        /// Top Contributor - achieved at 1500 points.
        /// </summary>
        TopContributor = 2,

        /// <summary>
        /// All-star Contributor - achieved at 5000 points.
        /// </summary>
        AllStarContributor = 3
    }
}
