using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;

namespace FreeSpeakWeb.Helpers;

/// <summary>
/// Static helper methods for comment-related operations shared across UI components.
/// Eliminates code duplication in PostDetailModal, GroupPostDetailModal, Home, Groups, etc.
/// </summary>
public static class CommentHelpers
{
    #region Display Name Helpers

    /// <summary>
    /// Get initials from a name for avatar display when no profile picture is available.
    /// Returns "U" for unknown/empty names.
    /// </summary>
    /// <param name="name">The full name to extract initials from</param>
    /// <returns>1-2 character uppercase initials</returns>
    public static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "U";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return "U";

        if (parts.Length == 1)
        {
            var singleWord = parts[0];
            if (singleWord.Length >= 2)
                return singleWord.Substring(0, 2).ToUpper();
            else
                return singleWord.Substring(0, 1).ToUpper();
        }

        // Use first letter of first name and last name
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    #endregion

    #region Comment Tree Navigation

    /// <summary>
    /// Recursively find a comment by ID in a comment tree.
    /// Searches through all replies at any depth level.
    /// </summary>
    /// <param name="comments">The list of comments to search</param>
    /// <param name="commentId">The ID of the comment to find</param>
    /// <returns>The matching CommentDisplayModel or null if not found</returns>
    public static CommentDisplayModel? FindCommentById(List<CommentDisplayModel> comments, int commentId)
    {
        foreach (var comment in comments)
        {
            if (comment.CommentId == commentId)
                return comment;

            if (comment.Replies != null)
            {
                var found = FindCommentById(comment.Replies, commentId);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Find the post ID that contains a specific comment from a dictionary of post comments.
    /// </summary>
    /// <param name="postComments">Dictionary mapping post IDs to their comment lists</param>
    /// <param name="commentId">The comment ID to find</param>
    /// <returns>The post ID containing the comment, or null if not found</returns>
    public static int? FindPostIdForComment(Dictionary<int, List<CommentDisplayModel>> postComments, int commentId)
    {
        foreach (var kvp in postComments)
        {
            if (FindCommentById(kvp.Value, commentId) != null)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    #endregion

    #region Comment Display Model Building

    /// <summary>
    /// Build a CommentDisplayModel from a Post Comment entity with all nested replies.
    /// </summary>
    /// <param name="comment">The Comment entity to build from</param>
    /// <param name="postService">PostService for loading replies and reaction data</param>
    /// <param name="userPreferenceService">UserPreferenceService for formatting display names</param>
    /// <param name="currentUserId">Current user ID for loading user-specific reaction data (optional)</param>
    /// <returns>A fully populated CommentDisplayModel</returns>
    public static async Task<CommentDisplayModel> BuildCommentDisplayModelAsync(
        Comment comment,
        PostService postService,
        UserPreferenceService userPreferenceService,
        string? currentUserId = null)
    {
        // Load replies for this comment
        var replies = await postService.GetRepliesAsync(comment.Id);
        var replyModels = new List<CommentDisplayModel>();

        foreach (var reply in replies)
        {
            var replyModel = await BuildCommentDisplayModelAsync(reply, postService, userPreferenceService, currentUserId);
            replyModels.Add(replyModel);
        }

        var authorName = await GetCommentAuthorNameAsync(comment.Author, userPreferenceService);

        return new CommentDisplayModel
        {
            CommentId = comment.Id,
            UserName = authorName,
            UserImageUrl = comment.Author?.ProfilePictureUrl,
            CommentAuthorId = comment.AuthorId,
            CommentText = comment.Content,
            Timestamp = comment.CreatedAt,
            Replies = replyModels.Count > 0 ? replyModels : null,
            LikeCount = await postService.GetCommentLikeCountAsync(comment.Id),
            UserReaction = currentUserId != null ? await postService.GetUserCommentReactionAsync(comment.Id, currentUserId) : null,
            ReactionBreakdown = await postService.GetCommentReactionBreakdownAsync(comment.Id)
        };
    }

    /// <summary>
    /// Build a CommentDisplayModel from a Group Post Comment entity with all nested replies.
    /// </summary>
    /// <param name="comment">The GroupPostComment entity to build from</param>
    /// <param name="groupPostService">GroupPostService for loading replies and reaction data</param>
    /// <param name="userPreferenceService">UserPreferenceService for formatting display names</param>
    /// <param name="currentUserId">Current user ID for loading user-specific reaction data (optional)</param>
    /// <returns>A fully populated CommentDisplayModel</returns>
    public static async Task<CommentDisplayModel> BuildCommentDisplayModelAsync(
        GroupPostComment comment,
        GroupPostService groupPostService,
        UserPreferenceService userPreferenceService,
        string? currentUserId = null)
    {
        // Load replies for this comment
        var replies = await groupPostService.GetRepliesAsync(comment.Id);
        var replyModels = new List<CommentDisplayModel>();

        foreach (var reply in replies)
        {
            var replyModel = await BuildCommentDisplayModelAsync(reply, groupPostService, userPreferenceService, currentUserId);
            replyModels.Add(replyModel);
        }

        var authorName = await GetCommentAuthorNameAsync(comment.Author, userPreferenceService);

        return new CommentDisplayModel
        {
            CommentId = comment.Id,
            UserName = authorName,
            UserImageUrl = comment.Author?.ProfilePictureUrl,
            CommentAuthorId = comment.AuthorId,
            CommentText = comment.Content,
            ImageUrl = comment.ImageUrl,
            Timestamp = comment.CreatedAt,
            Replies = replyModels.Count > 0 ? replyModels : null,
            LikeCount = await groupPostService.GetCommentLikeCountAsync(comment.Id),
            UserReaction = currentUserId != null ? await groupPostService.GetUserCommentReactionAsync(comment.Id, currentUserId) : null,
            ReactionBreakdown = await groupPostService.GetCommentReactionBreakdownAsync(comment.Id)
        };
    }

    /// <summary>
    /// Build CommentDisplayModels for a list of post comments with optimized batch loading.
    /// Loads ALL nested comments, user preferences, and reaction data in minimal queries.
    /// </summary>
    /// <param name="comments">The list of Comment entities to build from</param>
    /// <param name="postService">PostService for loading replies and reaction data</param>
    /// <param name="userPreferenceService">UserPreferenceService for formatting display names</param>
    /// <param name="currentUserId">Current user ID for loading user-specific reaction data (optional)</param>
    /// <returns>A list of fully populated CommentDisplayModels</returns>
    public static async Task<List<CommentDisplayModel>> BuildCommentDisplayModelsAsync(
        List<Comment> comments,
        PostService postService,
        UserPreferenceService userPreferenceService,
        string? currentUserId = null)
    {
        if (comments == null || comments.Count == 0)
            return new List<CommentDisplayModel>();

        // Get the postId from the first comment (all comments should belong to the same post)
        var postId = comments.First().PostId;

        // Step 1: Load ALL comments for the post in a SINGLE query (no more N+1!)
        var allComments = await postService.GetAllCommentsAsync(postId);
        var commentsById = allComments.ToDictionary(c => c.Id);

        // Step 2: Batch load user preferences (1 query)
        var userIds = allComments.Where(c => c.Author != null).Select(c => c.AuthorId).Distinct().ToList();
        var userPreferences = await userPreferenceService.GetNameDisplayTypesAsync(userIds);
        var userNames = allComments
            .Where(c => c.Author != null)
            .GroupBy(c => c.AuthorId)
            .ToDictionary(
                g => g.Key, 
                g => (g.First().Author!.FirstName, g.First().Author.LastName, g.First().Author.UserName ?? "Unknown"));
        var formattedNames = userPreferenceService.FormatUserDisplayNames(userNames, userPreferences);

        // Step 3: Batch load all reaction data (3 queries)
        var commentIdList = allComments.Select(c => c.Id).ToList();
        var likeCounts = await postService.GetCommentLikeCountsAsync(commentIdList);
        var userReactions = currentUserId != null 
            ? await postService.GetUserCommentReactionsAsync(currentUserId, commentIdList)
            : new Dictionary<int, LikeType?>();
        var reactionBreakdowns = await postService.GetCommentReactionBreakdownsAsync(commentIdList);

        // Step 4: Build models with all cached data (no more queries!)
        var result = new List<CommentDisplayModel>();
        foreach (var comment in comments)
        {
            var model = BuildCommentWithAllCachedData(
                comment,
                commentsById,
                formattedNames,
                likeCounts,
                userReactions,
                reactionBreakdowns);
            result.Add(model);
        }

        return result;
    }

    /// <summary>
    /// Build CommentDisplayModels for a list of group post comments with optimized batch loading.
    /// Loads ALL nested comments, user preferences, and reaction data in minimal queries.
    /// </summary>
    /// <param name="comments">The list of GroupPostComment entities to build from</param>
    /// <param name="groupPostService">GroupPostService for loading replies and reaction data</param>
    /// <param name="userPreferenceService">UserPreferenceService for formatting display names</param>
    /// <param name="currentUserId">Current user ID for loading user-specific reaction data (optional)</param>
    /// <returns>A list of fully populated CommentDisplayModels</returns>
    public static async Task<List<CommentDisplayModel>> BuildCommentDisplayModelsAsync(
        List<GroupPostComment> comments,
        GroupPostService groupPostService,
        UserPreferenceService userPreferenceService,
        string? currentUserId = null)
    {
        if (comments == null || comments.Count == 0)
            return new List<CommentDisplayModel>();

        // Get the postId from the first comment (all comments should belong to the same post)
        var postId = comments.First().PostId;

        // Step 1: Load ALL comments for the post in a SINGLE query (no more N+1!)
        var allComments = await groupPostService.GetAllCommentsAsync(postId);
        var commentsById = allComments.ToDictionary(c => c.Id);

        // Step 2: Batch load user preferences (1 query)
        var userIds = allComments.Where(c => c.Author != null).Select(c => c.AuthorId).Distinct().ToList();
        var userPreferences = await userPreferenceService.GetNameDisplayTypesAsync(userIds);
        var userNames = allComments
            .Where(c => c.Author != null)
            .GroupBy(c => c.AuthorId)
            .ToDictionary(
                g => g.Key, 
                g => (g.First().Author!.FirstName, g.First().Author.LastName, g.First().Author.UserName ?? "Unknown"));
        var formattedNames = userPreferenceService.FormatUserDisplayNames(userNames, userPreferences);

        // Step 3: Batch load all reaction data (3 queries)
        var commentIdList = allComments.Select(c => c.Id).ToList();
        var likeCounts = await groupPostService.GetCommentLikeCountsAsync(commentIdList);
        var userReactions = currentUserId != null 
            ? await groupPostService.GetUserCommentReactionsAsync(currentUserId, commentIdList)
            : new Dictionary<int, LikeType?>();
        var reactionBreakdowns = await groupPostService.GetCommentReactionBreakdownsAsync(commentIdList);

        // Step 4: Build models with all cached data (no more queries!)
        var result = new List<CommentDisplayModel>();
        foreach (var comment in comments)
        {
            var model = BuildCommentWithAllCachedData(
                comment,
                commentsById,
                formattedNames,
                likeCounts,
                userReactions,
                reactionBreakdowns);
            result.Add(model);
        }

        return result;
    }

    #endregion

    #region Batch Loading Helper Methods

    // Note: Removed CollectAllCommentIdsRecursive methods as they created N+1 queries.
    // Now using GetAllCommentsAsync to load all comments for a post in a single query.

    /// <summary>
    /// Build a CommentDisplayModel from a Comment entity using fully pre-loaded cached data.
    /// This method builds the entire tree structure without any database queries.
    /// </summary>
    private static CommentDisplayModel BuildCommentWithAllCachedData(
        Comment comment,
        Dictionary<int, Comment> allCommentsById,
        Dictionary<string, string> userDisplayNames,
        Dictionary<int, int> likeCounts,
        Dictionary<int, LikeType?> userReactions,
        Dictionary<int, Dictionary<LikeType, int>> reactionBreakdowns)
    {
        // Find all direct replies from the cached comments
        var replies = allCommentsById.Values
            .Where(c => c.ParentCommentId == comment.Id)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        var replyModels = new List<CommentDisplayModel>();
        foreach (var reply in replies)
        {
            var replyModel = BuildCommentWithAllCachedData(
                reply,
                allCommentsById,
                userDisplayNames,
                likeCounts,
                userReactions,
                reactionBreakdowns);
            replyModels.Add(replyModel);
        }

        var authorName = userDisplayNames.GetValueOrDefault(comment.AuthorId, "Unknown");

        return new CommentDisplayModel
        {
            CommentId = comment.Id,
            UserName = authorName,
            UserImageUrl = comment.Author?.ProfilePictureUrl,
            CommentAuthorId = comment.AuthorId,
            CommentText = comment.Content,
            Timestamp = comment.CreatedAt,
            Replies = replyModels.Count > 0 ? replyModels : null,
            LikeCount = likeCounts.GetValueOrDefault(comment.Id, 0),
            UserReaction = userReactions.GetValueOrDefault(comment.Id),
            ReactionBreakdown = reactionBreakdowns.GetValueOrDefault(comment.Id, new Dictionary<LikeType, int>())
        };
    }

    /// <summary>
    /// Build a CommentDisplayModel from a GroupPostComment entity using fully pre-loaded cached data.
    /// This method builds the entire tree structure without any database queries.
    /// </summary>
    private static CommentDisplayModel BuildCommentWithAllCachedData(
        GroupPostComment comment,
        Dictionary<int, GroupPostComment> allCommentsById,
        Dictionary<string, string> userDisplayNames,
        Dictionary<int, int> likeCounts,
        Dictionary<int, LikeType?> userReactions,
        Dictionary<int, Dictionary<LikeType, int>> reactionBreakdowns)
    {
        // Find all direct replies from the cached comments
        var replies = allCommentsById.Values
            .Where(c => c.ParentCommentId == comment.Id)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        var replyModels = new List<CommentDisplayModel>();
        foreach (var reply in replies)
        {
            var replyModel = BuildCommentWithAllCachedData(
                reply,
                allCommentsById,
                userDisplayNames,
                likeCounts,
                userReactions,
                reactionBreakdowns);
            replyModels.Add(replyModel);
        }

        var authorName = userDisplayNames.GetValueOrDefault(comment.AuthorId, "Unknown");

        return new CommentDisplayModel
        {
            CommentId = comment.Id,
            UserName = authorName,
            UserImageUrl = comment.Author?.ProfilePictureUrl,
            CommentAuthorId = comment.AuthorId,
            CommentText = comment.Content,
            ImageUrl = comment.ImageUrl,
            Timestamp = comment.CreatedAt,
            Replies = replyModels.Count > 0 ? replyModels : null,
            LikeCount = likeCounts.GetValueOrDefault(comment.Id, 0),
            UserReaction = userReactions.GetValueOrDefault(comment.Id),
            ReactionBreakdown = reactionBreakdowns.GetValueOrDefault(comment.Id, new Dictionary<LikeType, int>())
        };
    }

    #endregion

    #region Author Name Formatting

    /// <summary>
    /// Get the formatted display name for a comment author based on their preferences.
    /// </summary>
    /// <param name="author">The ApplicationUser author (can be null)</param>
    /// <param name="userPreferenceService">Service for formatting the display name</param>
    /// <returns>The formatted display name or "Unknown" if author is null</returns>
    public static async Task<string> GetCommentAuthorNameAsync(
        ApplicationUser? author,
        UserPreferenceService userPreferenceService)
    {
        if (author == null) return "Unknown";

        return await userPreferenceService.FormatUserDisplayNameAsync(
            author.Id,
            author.FirstName,
            author.LastName,
            author.UserName ?? "Unknown"
        );
    }

    #endregion

    #region Relative Timestamp Formatting

    /// <summary>
    /// Format a DateTime as a human-readable relative timestamp (e.g., "5m ago", "2h ago", "3d ago").
    /// </summary>
    /// <param name="timestamp">The timestamp to format</param>
    /// <returns>A human-readable relative time string</returns>
    public static string FormatRelativeTimestamp(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var utcTimestamp = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        var difference = now - utcTimestamp;

        if (difference.TotalSeconds < 60)
            return "just now";

        if (difference.TotalMinutes < 60)
            return $"{(int)difference.TotalMinutes}m ago";

        if (difference.TotalHours < 24)
            return $"{(int)difference.TotalHours}h ago";

        if (difference.TotalDays < 7)
            return $"{(int)difference.TotalDays}d ago";

        if (difference.TotalDays < 30)
            return $"{(int)(difference.TotalDays / 7)}w ago";

        if (difference.TotalDays < 365)
            return $"{(int)(difference.TotalDays / 30)}mo ago";

        return $"{(int)(difference.TotalDays / 365)}y ago";
    }

    #endregion
}
