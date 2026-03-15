using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;

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
    [Inject] protected AlertService AlertService { get; set; } = default!;
    [Inject] protected IAuditLogRepository AuditLogRepository { get; set; } = default!;

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

    /// <summary>
    /// Deletes a comment from the post.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="commentId">The ID of the comment to delete</param>
    /// <param name="userId">The ID of the user requesting the deletion (must be the author)</param>
    /// <returns>Tuple containing success status, optional error message, and the count of deleted comments (including nested replies)</returns>
    protected abstract Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteCommentAsync(
        int commentId,
        string userId);

    /// <summary>
    /// Updates a comment's content.
    /// Implementation should call the appropriate service (PostService or GroupPostService).
    /// </summary>
    /// <param name="commentId">The ID of the comment to update</param>
    /// <param name="userId">The ID of the user requesting the update (must be the author)</param>
    /// <param name="newContent">The new content for the comment</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> UpdateCommentAsync(
        int commentId,
        string userId,
        string newContent);

    /// <summary>
    /// Reports a comment for moderation.
    /// Implementation should call the appropriate service.
    /// </summary>
    /// <param name="commentId">The ID of the comment to report</param>
    /// <param name="userId">The ID of the user making the report</param>
    /// <returns>Tuple containing success status and optional error message</returns>
    protected abstract Task<(bool Success, string? ErrorMessage)> ReportCommentAsync(
        int commentId,
        string userId);

    /// <summary>
    /// Decrements the comment count for the specified post in the page's post list.
    /// </summary>
    /// <param name="postId">The ID of the post to update</param>
    /// <param name="decrementBy">The number to decrement by (default 1)</param>
    protected abstract void DecrementPostCommentCount(int postId, int decrementBy = 1);

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

                // Log comment creation to audit log
                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserComment, new UserCommentDetails
                    {
                        CommentId = 0, // Will be set by the service
                        PostId = args.PostId,
                        OperationType = OperationTypeEnum.Create.ToString()
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
            else
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to add comment.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while adding your comment.");
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
                return;
            }

            int postId = GetPostIdFromComment(parentComment);

            var result = await AddReplyToPostAsync(postId, CurrentUserId, args.Content, null, args.ParentCommentId);

            if (!result.Success)
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to add reply.");
                return;
            }

            IncrementPostCommentCount(postId);
            IncrementRefreshTrigger(postId);

            // Log reply creation to audit log
            try
            {
                await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserComment, new UserCommentDetails
                {
                    CommentId = 0, // Will be set by the service
                    PostId = postId,
                    OperationType = OperationTypeEnum.Reply.ToString(),
                    ParentCommentId = args.ParentCommentId
                });
            }
            catch { /* Audit logging should not fail the operation */ }

            StateHasChanged();
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while adding your reply.");
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
            else
            {
                AlertService.ShowError("Failed to add reaction.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while adding your reaction.");
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
            else
            {
                AlertService.ShowError("Failed to remove reaction.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while removing your reaction.");
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
            else
            {
                AlertService.ShowError("Failed to add reaction.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while adding your reaction.");
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
            else
            {
                AlertService.ShowError("Failed to remove reaction.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while removing your reaction.");
        }
    }

    /// <summary>
    /// Handles when a user requests to delete their own comment.
    /// Deletes the comment and all nested replies, updating the UI state.
    /// </summary>
    /// <param name="commentId">The ID of the comment to delete</param>
    protected async Task HandleDeleteComment(int commentId)
    {
        if (CurrentUserId == null) return;

        try
        {
            // Find the post before deleting so we can refresh
            var postId = await FindPostIdForCommentAsync(commentId);

            var result = await DeleteCommentAsync(commentId, CurrentUserId);

            if (result.Success)
            {
                if (postId.HasValue)
                {
                    // Decrement by the total number of deleted comments (including nested replies)
                    DecrementPostCommentCount(postId.Value, result.DeletedCount);
                    IncrementRefreshTrigger(postId.Value);
                }

                // Note: Audit logging for comment deletion is handled by the service/repository layer
                // to ensure proper context (e.g., GroupId for group comments) is included.

                StateHasChanged();
            }
            else
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to delete comment.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while deleting the comment.");
        }
    }

    /// <summary>
    /// Handles when a user requests to edit their own comment.
    /// This method is kept for backward compatibility but is no longer used with inline editing.
    /// The MultiLineCommentDisplay component now handles edit mode internally.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and CurrentContent</param>
    [Obsolete("This method is deprecated. Inline editing is now handled by MultiLineCommentDisplay component.")]
    protected virtual async Task HandleEditComment((int CommentId, string CurrentContent) args)
    {
        // This method is now obsolete as inline editing is handled by the MultiLineCommentDisplay component
        // Kept for backward compatibility only
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles when a comment is updated via inline editing.
    /// This is the new handler called by MultiLineCommentDisplay when save is clicked.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and NewContent</param>
    protected async Task HandleCommentUpdated((int CommentId, string NewContent) args)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await UpdateCommentAsync(args.CommentId, CurrentUserId, args.NewContent);

            if (result.Success)
            {
                var postId = await FindPostIdForCommentAsync(args.CommentId);

                if (postId.HasValue)
                {
                    IncrementRefreshTrigger(postId.Value);
                }

                // Log comment update to audit log
                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserComment, new UserCommentDetails
                    {
                        CommentId = args.CommentId,
                        PostId = postId ?? 0,
                        OperationType = OperationTypeEnum.Edit.ToString()
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
            else
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to update comment.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while updating the comment.");
        }
    }

    /// <summary>
    /// Handles the actual update of a comment's content.
    /// </summary>
    /// <param name="commentId">The ID of the comment to update</param>
    /// <param name="newContent">The new content for the comment</param>
    protected async Task HandleUpdateComment(int commentId, string newContent)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await UpdateCommentAsync(commentId, CurrentUserId, newContent);

            if (result.Success)
            {
                var postId = await FindPostIdForCommentAsync(commentId);

                if (postId.HasValue)
                {
                    IncrementRefreshTrigger(postId.Value);
                }

                // Log comment update to audit log
                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserComment, new UserCommentDetails
                    {
                        CommentId = commentId,
                        PostId = postId ?? 0,
                        OperationType = OperationTypeEnum.Edit.ToString()
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
            else
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to update comment.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while updating the comment.");
        }
    }

    /// <summary>
    /// Handles when a user requests to report another user's comment.
    /// </summary>
    /// <param name="commentId">The ID of the comment to report</param>
    protected async Task HandleReportComment(int commentId)
    {
        if (CurrentUserId == null) return;

        try
        {
            var result = await ReportCommentAsync(commentId, CurrentUserId);

            if (result.Success)
            {
                // Report submitted successfully - no alert needed
            }
            else
            {
                AlertService.ShowError(result.ErrorMessage ?? "Failed to submit report.");
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("An error occurred while submitting the report.");
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
