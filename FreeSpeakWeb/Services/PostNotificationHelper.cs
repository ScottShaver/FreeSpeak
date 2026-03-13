using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Centralized helper service for creating post-related notifications.
    /// Used by both PostService and GroupPostService to eliminate notification code duplication.
    /// </summary>
    public class PostNotificationHelper
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly NotificationService _notificationService;
        private readonly UserPreferenceService _userPreferenceService;
        private readonly ILogger<PostNotificationHelper> _logger;

        public PostNotificationHelper(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            NotificationService notificationService,
            UserPreferenceService userPreferenceService,
            ILogger<PostNotificationHelper> logger)
        {
            _contextFactory = contextFactory;
            _notificationService = notificationService;
            _userPreferenceService = userPreferenceService;
            _logger = logger;
        }

        /// <summary>
        /// Send notification for a new comment on a post
        /// </summary>
        public async Task NotifyPostCommentAsync(
            string postAuthorId,
            string commenterId,
            int postId,
            int commentId,
            NotificationType notificationType,
            int? groupId = null,
            bool checkMute = true)
        {
            if (postAuthorId == commenterId)
                return;

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check mute status for regular posts
                if (checkMute && groupId == null)
                {
                    var isMuted = await context.PostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == postAuthorId);
                    if (isMuted) return;
                }

                // Check mute status for group posts
                if (checkMute && groupId.HasValue)
                {
                    var isMuted = await context.GroupPostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == postAuthorId);
                    if (isMuted) return;
                }

                var commenter = await context.Users.FindAsync(commenterId);
                if (commenter == null) return;

                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    commenter.Id,
                    commenter.FirstName ?? string.Empty,
                    commenter.LastName ?? string.Empty,
                    commenter.UserName ?? "User"
                );

                var message = groupId.HasValue
                    ? $"<strong>{formattedName}</strong> commented on your group post"
                    : $"<strong>{formattedName}</strong> commented on your post";

                var data = groupId.HasValue
                    ? new
                    {
                        GroupPostId = postId,
                        GroupId = groupId.Value,
                        CommentId = commentId,
                        CommenterId = commenterId,
                        CommenterName = formattedName,
                        CommenterProfilePicture = commenter.ProfilePictureUrl
                    }
                    : (object)new
                    {
                        PostId = postId,
                        CommentId = commentId,
                        CommenterId = commenterId,
                        CommenterName = formattedName,
                        CommenterProfilePicture = commenter.ProfilePictureUrl
                    };

                await _notificationService.CreateNotificationAsync(
                    postAuthorId,
                    notificationType,
                    message,
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending comment notification for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Send notification for a reply to a comment
        /// </summary>
        public async Task NotifyCommentReplyAsync(
            string parentCommentAuthorId,
            string replierId,
            int postId,
            int commentId,
            NotificationType notificationType,
            int? groupId = null,
            bool checkMute = true)
        {
            if (parentCommentAuthorId == replierId)
                return;

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check mute status for regular posts
                if (checkMute && groupId == null)
                {
                    var isMuted = await context.PostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == parentCommentAuthorId);
                    if (isMuted) return;
                }

                // Check mute status for group posts
                if (checkMute && groupId.HasValue)
                {
                    var isMuted = await context.GroupPostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == parentCommentAuthorId);
                    if (isMuted) return;
                }

                var replier = await context.Users.FindAsync(replierId);
                if (replier == null) return;

                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    replier.Id,
                    replier.FirstName ?? string.Empty,
                    replier.LastName ?? string.Empty,
                    replier.UserName ?? "User"
                );

                var message = groupId.HasValue
                    ? $"<strong>{formattedName}</strong> replied to your comment in a group"
                    : $"<strong>{formattedName}</strong> replied to your comment";

                var data = groupId.HasValue
                    ? new
                    {
                        GroupPostId = postId,
                        GroupId = groupId.Value,
                        CommentId = commentId,
                        CommenterId = replierId,
                        CommenterName = formattedName,
                        CommenterProfilePicture = replier.ProfilePictureUrl
                    }
                    : (object)new
                    {
                        PostId = postId,
                        CommentId = commentId,
                        CommenterId = replierId,
                        CommenterName = formattedName,
                        CommenterProfilePicture = replier.ProfilePictureUrl
                    };

                await _notificationService.CreateNotificationAsync(
                    parentCommentAuthorId,
                    notificationType,
                    message,
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending comment reply notification for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Send notification for a reaction on a post
        /// </summary>
        public async Task NotifyPostReactionAsync(
            string postAuthorId,
            string reactorId,
            int postId,
            LikeType reactionType,
            NotificationType notificationType,
            int? groupId = null,
            bool checkMute = true)
        {
            if (postAuthorId == reactorId)
                return;

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check mute status for regular posts
                if (checkMute && groupId == null)
                {
                    var isMuted = await context.PostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == postAuthorId);
                    if (isMuted) return;
                }

                // Check mute status for group posts
                if (checkMute && groupId.HasValue)
                {
                    var isMuted = await context.GroupPostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == postAuthorId);
                    if (isMuted) return;
                }

                var reactor = await context.Users.FindAsync(reactorId);
                if (reactor == null) return;

                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    reactor.Id,
                    reactor.FirstName ?? string.Empty,
                    reactor.LastName ?? string.Empty,
                    reactor.UserName ?? "User"
                );

                var reactionText = reactionType.ToString().ToLower();
                var message = groupId.HasValue
                    ? $"<strong>{formattedName}</strong> reacted to your group post with {reactionText}"
                    : $"<strong>{formattedName}</strong> reacted to your post with {reactionText}";

                var data = groupId.HasValue
                    ? new
                    {
                        GroupPostId = postId,
                        GroupId = groupId.Value,
                        ReactorId = reactorId,
                        ReactorName = formattedName,
                        ReactorProfilePicture = reactor.ProfilePictureUrl,
                        ReactionType = reactionType.ToString()
                    }
                    : (object)new
                    {
                        PostId = postId,
                        ReactorId = reactorId,
                        ReactorName = formattedName,
                        ReactorProfilePicture = reactor.ProfilePictureUrl,
                        ReactionType = reactionType.ToString()
                    };

                await _notificationService.CreateNotificationAsync(
                    postAuthorId,
                    notificationType,
                    message,
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending post reaction notification for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Send notification for a reaction on a comment
        /// </summary>
        public async Task NotifyCommentReactionAsync(
            string commentAuthorId,
            string reactorId,
            int postId,
            int commentId,
            LikeType reactionType,
            NotificationType notificationType,
            int? groupId = null,
            bool checkMute = true)
        {
            if (commentAuthorId == reactorId)
                return;

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check mute status for regular posts
                if (checkMute && groupId == null)
                {
                    var isMuted = await context.PostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == commentAuthorId);
                    if (isMuted) return;
                }

                // Check mute status for group posts
                if (checkMute && groupId.HasValue)
                {
                    var isMuted = await context.GroupPostNotificationMutes
                        .AnyAsync(m => m.PostId == postId && m.UserId == commentAuthorId);
                    if (isMuted) return;
                }

                var reactor = await context.Users.FindAsync(reactorId);
                if (reactor == null) return;

                var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    reactor.Id,
                    reactor.FirstName ?? string.Empty,
                    reactor.LastName ?? string.Empty,
                    reactor.UserName ?? "User"
                );

                var reactionText = reactionType.ToString().ToLower();
                var message = groupId.HasValue
                    ? $"<strong>{formattedName}</strong> reacted to your comment in a group with {reactionText}"
                    : $"<strong>{formattedName}</strong> reacted to your comment with {reactionText}";

                var data = groupId.HasValue
                    ? new
                    {
                        GroupPostId = postId,
                        GroupId = groupId.Value,
                        CommentId = commentId,
                        ReactorId = reactorId,
                        ReactorName = formattedName,
                        ReactorProfilePicture = reactor.ProfilePictureUrl,
                        ReactionType = reactionType.ToString()
                    }
                    : (object)new
                    {
                        PostId = postId,
                        CommentId = commentId,
                        ReactorId = reactorId,
                        ReactorName = formattedName,
                        ReactorProfilePicture = reactor.ProfilePictureUrl,
                        ReactionType = reactionType.ToString()
                    };

                await _notificationService.CreateNotificationAsync(
                    commentAuthorId,
                    notificationType,
                    message,
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending comment reaction notification for comment {CommentId}", commentId);
            }
        }
    }
}
