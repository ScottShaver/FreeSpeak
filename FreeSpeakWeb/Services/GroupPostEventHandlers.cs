using FreeSpeakWeb.Data;
using FreeSpeakWeb.Components.SocialFeed;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Shared event handlers for group post-related operations across multiple components.
    /// Reduces code duplication in Groups.razor, GroupView.razor, and other pages.
    /// </summary>
    public class GroupPostEventHandlers
    {
        private readonly GroupPostService _groupPostService;
        private readonly ILogger<GroupPostEventHandlers> _logger;

        public GroupPostEventHandlers(
            GroupPostService groupPostService,
            ILogger<GroupPostEventHandlers> logger)
        {
            _groupPostService = groupPostService;
            _logger = logger;
        }

        /// <summary>
        /// Handles comment addition by updating post count and reloading comments
        /// </summary>
        public async Task HandleCommentAddedAsync(
            int postId,
            List<GroupPost> posts,
            List<GroupPost>? pinnedPosts,
            Dictionary<int, List<CommentDisplayModel>> commentsDict,
            Dictionary<int, int> directCommentCounts,
            Func<int, int, Task> loadCommentsFunc)
        {
            try
            {
                // Update comment count in main posts list
                var post = posts.FirstOrDefault(p => p.Id == postId);
                if (post != null)
                {
                    post.CommentCount++;
                }

                // Also check pinned posts list if provided
                if (pinnedPosts != null)
                {
                    var pinnedPost = pinnedPosts.FirstOrDefault(p => p.Id == postId);
                    if (pinnedPost != null)
                    {
                        pinnedPost.CommentCount++;
                    }
                }

                // Reload comments to show the new one
                await loadCommentsFunc(postId, 3);

                // Update direct comment count
                var directCount = await _groupPostService.GetDirectCommentCountAsync(postId);
                directCommentCounts[postId] = directCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling comment added for post {PostId}", postId);
                throw;
            }
        }

        /// <summary>
        /// Handles reply submission by finding the parent post and reloading comments
        /// </summary>
        public async Task HandleReplySubmittedAsync(
            int parentCommentId,
            List<GroupPost> posts,
            List<GroupPost>? pinnedPosts,
            Dictionary<int, List<CommentDisplayModel>> commentsDict,
            Func<int, int, Task> loadCommentsFunc)
        {
            try
            {
                // Find the post ID by searching through comments
                int? postId = FindPostIdForComment(parentCommentId, commentsDict);

                if (postId == null)
                {
                    _logger.LogWarning("Could not find post for parent comment {ParentCommentId}", parentCommentId);
                    return;
                }

                // Reload comments for this post to show the new reply
                await loadCommentsFunc(postId.Value, 3);

                // Update comment count
                var post = posts.FirstOrDefault(p => p.Id == postId.Value);
                if (post != null)
                {
                    post.CommentCount++;
                }

                // Also check pinned posts list
                if (pinnedPosts != null)
                {
                    var pinnedPost = pinnedPosts.FirstOrDefault(p => p.Id == postId.Value);
                    if (pinnedPost != null)
                    {
                        pinnedPost.CommentCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling reply submitted for parent comment {ParentCommentId}", parentCommentId);
                throw;
            }
        }

        /// <summary>
        /// Handles reaction changes on group posts
        /// </summary>
        public async Task HandleReactionChangedAsync(
            int postId,
            string userId,
            LikeType reactionType,
            List<GroupPost> posts,
            Dictionary<int, LikeType?> userReactions,
            Dictionary<int, Dictionary<LikeType, int>> reactionData)
        {
            try
            {
                var result = await _groupPostService.AddOrUpdateReactionAsync(postId, userId, reactionType);

                if (result.Success)
                {
                    var previousReaction = userReactions.ContainsKey(postId) ? userReactions[postId] : null;
                    userReactions[postId] = reactionType;

                    var reactions = await _groupPostService.GetReactionBreakdownAsync(postId);
                    reactionData[postId] = reactions;

                    var post = posts.FirstOrDefault(p => p.Id == postId);
                    if (post != null && previousReaction == null)
                    {
                        post.LikeCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling reaction change for post {PostId}", postId);
                throw;
            }
        }

        /// <summary>
        /// Handles reaction removal on group posts
        /// </summary>
        public async Task HandleRemoveReactionAsync(
            int postId,
            string userId,
            List<GroupPost> posts,
            Dictionary<int, LikeType?> userReactions,
            Dictionary<int, Dictionary<LikeType, int>> reactionData)
        {
            try
            {
                var result = await _groupPostService.RemoveReactionAsync(postId, userId);

                if (result.Success)
                {
                    userReactions[postId] = null;

                    var reactions = await _groupPostService.GetReactionBreakdownAsync(postId);
                    reactionData[postId] = reactions;

                    var post = posts.FirstOrDefault(p => p.Id == postId);
                    if (post != null)
                    {
                        post.LikeCount = Math.Max(0, post.LikeCount - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction for post {PostId}", postId);
                throw;
            }
        }

        /// <summary>
        /// Handles comment reaction changes
        /// </summary>
        public async Task HandleCommentReactionChangedAsync(
            int commentId,
            string userId,
            LikeType reactionType,
            Dictionary<int, List<CommentDisplayModel>> commentsDict)
        {
            try
            {
                var result = await _groupPostService.AddOrUpdateCommentReactionAsync(commentId, userId, reactionType);

                if (result.Success)
                {
                    await UpdateCommentReactionData(commentId, reactionType, commentsDict);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling comment reaction for comment {CommentId}", commentId);
                throw;
            }
        }

        /// <summary>
        /// Handles comment reaction removal
        /// </summary>
        public async Task HandleRemoveCommentReactionAsync(
            int commentId,
            string userId,
            Dictionary<int, List<CommentDisplayModel>> commentsDict)
        {
            try
            {
                var result = await _groupPostService.RemoveCommentReactionAsync(commentId, userId);

                if (result.Success)
                {
                    await UpdateCommentReactionData(commentId, null, commentsDict);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing comment reaction for comment {CommentId}", commentId);
                throw;
            }
        }

        #region Helper Methods

        private int? FindPostIdForComment(int commentId, Dictionary<int, List<CommentDisplayModel>> commentsDict)
        {
            foreach (var kvp in commentsDict)
            {
                if (FindCommentById(kvp.Value, commentId) != null)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private CommentDisplayModel? FindCommentById(List<CommentDisplayModel> comments, int commentId)
        {
            foreach (var comment in comments)
            {
                if (comment.CommentId == commentId)
                {
                    return comment;
                }

                if (comment.Replies != null && comment.Replies.Any())
                {
                    var found = FindCommentById(comment.Replies, commentId);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        private async Task UpdateCommentReactionData(
            int commentId,
            LikeType? reactionType,
            Dictionary<int, List<CommentDisplayModel>> commentsDict)
        {
            foreach (var kvp in commentsDict)
            {
                var comment = FindCommentById(kvp.Value, commentId);
                if (comment != null)
                {
                    comment.UserReaction = reactionType;

                    if (reactionType.HasValue)
                    {
                        if (comment.ReactionBreakdown == null)
                        {
                            comment.ReactionBreakdown = new Dictionary<LikeType, int>();
                        }

                        if (!comment.ReactionBreakdown.ContainsKey(reactionType.Value))
                        {
                            comment.ReactionBreakdown[reactionType.Value] = 0;
                        }
                        comment.ReactionBreakdown[reactionType.Value]++;
                        comment.LikeCount++;
                    }
                    else
                    {
                        comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
                    }
                    break;
                }
            }
        }

        #endregion
    }
}
