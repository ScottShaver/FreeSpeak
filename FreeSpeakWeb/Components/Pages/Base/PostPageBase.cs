using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Components.Pages.Base;

/// <summary>
/// Generic base component for pages that display posts and handle user interactions.
/// Works with both regular posts (Post/Comment) and group posts (GroupPost/GroupPostComment).
/// Consolidates duplicated handler logic across multiple page components.
/// </summary>
/// <typeparam name="TPost">The post entity type (Post or GroupPost)</typeparam>
/// <typeparam name="TComment">The comment entity type (Comment or GroupPostComment)</typeparam>
public abstract class PostPageBase<TPost, TComment> : ComponentBase
    where TPost : class
    where TComment : class
{
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

    // Dictionaries for managing post state
    protected Dictionary<int, int> postRefreshTriggers = new();
    protected Dictionary<int, Dictionary<LikeType, int>> postReactionData = new();
    protected Dictionary<int, LikeType?> postUserReactions = new();
    protected Dictionary<int, bool> pinnedPosts = new();
    protected Dictionary<int, string> postAuthorNames = new();

    #region Abstract Properties - Implemented by Derived Pages

    /// <summary>
    /// Gets the current authenticated user's ID
    /// </summary>
    protected abstract string? CurrentUserId { get; }

    /// <summary>
    /// Gets the current authenticated user's profile image URL
    /// </summary>
    protected abstract string? CurrentUserImageUrl { get; }

    /// <summary>
    /// Gets the current authenticated user's display name
    /// </summary>
    protected abstract string? CurrentUserName { get; }

    #endregion

    #region Abstract Service Bridge Methods

    /// <summary>
    /// Adds a comment to the specified post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="postId">The ID of the post to comment on</param>
    /// <param name="userId">The ID of the user creating the comment</param>
    /// <param name="content">The comment text content</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> AddCommentToPostAsync(
        int postId, 
        string userId, 
        string content);

    /// <summary>
    /// Adds a reply to a comment on the specified post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="postId">The ID of the post containing the comment</param>
    /// <param name="userId">The ID of the user creating the reply</param>
    /// <param name="content">The reply text content</param>
    /// <param name="imageId">Optional image ID attached to the reply</param>
    /// <param name="parentCommentId">The ID of the parent comment being replied to</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> AddReplyToPostAsync(
        int postId, 
        string userId, 
        string content, 
        int? imageId, 
        int parentCommentId);

    /// <summary>
    /// Gets a comment by its ID.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="commentId">The ID of the comment to retrieve</param>
    /// <returns>The comment entity, or null if not found</returns>
    protected abstract Task<TComment?> GetCommentByIdAsync(int commentId);

    /// <summary>
    /// Extracts the post ID from a comment entity.
    /// </summary>
    /// <param name="comment">The comment entity</param>
    /// <returns>The post ID that the comment belongs to</returns>
    protected abstract int GetPostIdFromComment(TComment comment);

    /// <summary>
    /// Increments the comment count for the specified post in the page's post list.
    /// </summary>
    /// <param name="postId">The ID of the post to update</param>
    protected abstract void IncrementPostCommentCount(int postId);

    /// <summary>
    /// Adds or updates a reaction on the specified post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="postId">The ID of the post to react to</param>
    /// <param name="userId">The ID of the user adding the reaction</param>
    /// <param name="reactionType">The type of reaction to add</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> AddOrUpdatePostReactionAsync(
        int postId, 
        string userId, 
        LikeType reactionType);

    /// <summary>
    /// Removes a reaction from the specified post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="postId">The ID of the post to remove reaction from</param>
    /// <param name="userId">The ID of the user removing the reaction</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> RemovePostReactionAsync(
        int postId, 
        string userId);

    /// <summary>
    /// Gets the reaction breakdown for the specified post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="postId">The ID of the post</param>
    /// <returns>Dictionary mapping reaction types to their counts</returns>
    protected abstract Task<Dictionary<LikeType, int>> GetPostReactionBreakdownAsync(int postId);

    /// <summary>
    /// Increments the like count for the specified post in the page's post list.
    /// </summary>
    /// <param name="postId">The ID of the post to update</param>
    protected abstract void IncrementPostLikeCount(int postId);

    /// <summary>
    /// Decrements the like count for the specified post in the page's post list.
    /// </summary>
    /// <param name="postId">The ID of the post to update</param>
    protected abstract void DecrementPostLikeCount(int postId);

    /// <summary>
    /// Adds or updates a reaction on the specified comment.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="commentId">The ID of the comment to react to</param>
    /// <param name="userId">The ID of the user adding the reaction</param>
    /// <param name="reactionType">The type of reaction to add</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(
        int commentId, 
        string userId, 
        LikeType reactionType);

    /// <summary>
    /// Removes a reaction from the specified comment.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="commentId">The ID of the comment to remove reaction from</param>
    /// <param name="userId">The ID of the user removing the reaction</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> RemoveCommentReactionAsync(
        int commentId, 
        string userId);

    /// <summary>
    /// Finds the post ID that contains the specified comment.
    /// Used to trigger refresh after comment reactions change.
    /// </summary>
    /// <param name="commentId">The ID of the comment</param>
    /// <returns>The post ID if found, null otherwise</returns>
    protected abstract Task<int?> FindPostIdForCommentAsync(int commentId);

    #endregion

    #region Shared Comment Handler Methods

    /// <summary>
    /// Handles when a new comment is added to a post.
    /// This method is called from child components via event callbacks.
    /// </summary>
    /// <param name="args">Tuple containing the PostId and comment Content</param>
    protected async Task HandleCommentAdded((int PostId, string Content) args)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await AddCommentToPostAsync(args.PostId, CurrentUserId, args.Content);

            if (result.Success)
            {
                IncrementPostCommentCount(args.PostId);
                IncrementRefreshTrigger(args.PostId);
                StateHasChanged();
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", result.ErrorMessage ?? "Failed to add comment");
                Console.WriteLine($"Failed to add comment: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", "An error occurred while adding your comment.");
            Console.WriteLine($"Error adding comment: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles when a reply is submitted to an existing comment.
    /// This method is called from child components via event callbacks.
    /// </summary>
    /// <param name="args">Tuple containing the ParentCommentId and reply Content</param>
    protected async Task HandleReplySubmitted((int ParentCommentId, string Content) args)
    {
        if (CurrentUserId == null) return;

        try
        {
            var parentComment = await GetCommentByIdAsync(args.ParentCommentId);
            if (parentComment == null)
            {
                Console.WriteLine($"Could not find parent comment {args.ParentCommentId}");
                return;
            }

            int postId = GetPostIdFromComment(parentComment);

            var result = await AddReplyToPostAsync(postId, CurrentUserId, args.Content, null, args.ParentCommentId);

            if (!result.Success)
            {
                await JSRuntime.InvokeVoidAsync("alert", result.ErrorMessage ?? "Failed to add reply");
                Console.WriteLine($"Failed to add reply: {result.ErrorMessage}");
                return;
            }

            IncrementPostCommentCount(postId);
            IncrementRefreshTrigger(postId);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating UI after reply added: {ex.Message}");
        }
    }

    #endregion

    #region Shared Reaction Handler Methods

    /// <summary>
    /// Handles when a user adds or changes their reaction on a post.
    /// </summary>
    /// <param name="postId">The ID of the post being reacted to</param>
    /// <param name="reactionType">The type of reaction</param>
    protected async Task HandlePostReactionChanged(int postId, LikeType reactionType)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await AddOrUpdatePostReactionAsync(postId, CurrentUserId, reactionType);

            if (result.Success)
            {
                var previousReaction = postUserReactions.ContainsKey(postId) ? postUserReactions[postId] : null;
                postUserReactions[postId] = reactionType;

                var reactions = await GetPostReactionBreakdownAsync(postId);
                postReactionData[postId] = reactions;

                // Only increment count if user didn't have a reaction before
                if (previousReaction == null)
                {
                    IncrementPostLikeCount(postId);
                }

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling post reaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles when a user removes their reaction from a post.
    /// </summary>
    /// <param name="postId">The ID of the post to remove reaction from</param>
    protected async Task HandleRemovePostReaction(int postId)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await RemovePostReactionAsync(postId, CurrentUserId);

            if (result.Success)
            {
                postUserReactions[postId] = null;

                var reactions = await GetPostReactionBreakdownAsync(postId);
                postReactionData[postId] = reactions;

                DecrementPostLikeCount(postId);

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing post reaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles when a user adds or changes their reaction on a comment.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and ReactionType</param>
    protected async Task HandleCommentReactionChanged((int CommentId, LikeType ReactionType) args)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await AddOrUpdateCommentReactionAsync(args.CommentId, CurrentUserId, args.ReactionType);

            if (result.Success)
            {
                var postId = await FindPostIdForCommentAsync(args.CommentId);

                if (postId.HasValue)
                {
                    IncrementRefreshTrigger(postId.Value);
                }

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling comment reaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles when a user removes their reaction from a comment.
    /// </summary>
    /// <param name="commentId">The ID of the comment to remove reaction from</param>
    protected async Task HandleRemoveCommentReaction(int commentId)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await RemoveCommentReactionAsync(commentId, CurrentUserId);

            if (result.Success)
            {
                var postId = await FindPostIdForCommentAsync(commentId);

                if (postId.HasValue)
                {
                    IncrementRefreshTrigger(postId.Value);
                }

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing comment reaction: {ex.Message}");
        }
    }

    #endregion

    #region Shared Helper Methods

    /// <summary>
    /// Increments the refresh trigger for a post, causing child components to reload.
    /// This is used to force FeedArticle or GroupPostArticle components to refresh their comments.
    /// </summary>
    /// <param name="postId">The ID of the post to refresh</param>
    protected void IncrementRefreshTrigger(int postId)
    {
        if (!postRefreshTriggers.ContainsKey(postId))
        {
            postRefreshTriggers[postId] = 0;
        }
        postRefreshTriggers[postId]++;
    }

    #endregion
}
