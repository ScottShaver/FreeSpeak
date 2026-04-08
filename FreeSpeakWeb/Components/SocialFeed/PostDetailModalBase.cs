using FreeSpeakWeb.Data;
using FreeSpeakWeb.Helpers;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Base class for post detail modals providing shared state, parameters, and lifecycle management.
/// Derived classes (PostDetailModal, GroupPostDetailModal) override abstract methods for service-specific calls.
/// </summary>
public abstract class PostDetailModalBase : ComponentBase, IAsyncDisposable
{
    #region Injected Services

    /// <summary>
    /// JavaScript runtime for interop calls.
    /// </summary>
    [Inject]
    protected IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// User preference service for display name formatting.
    /// </summary>
    [Inject]
    protected UserPreferenceService UserPreferenceService { get; set; } = default!;

    /// <summary>
    /// Site settings configuration options.
    /// </summary>
    [Inject]
    protected Microsoft.Extensions.Options.IOptions<SiteSettings> SiteSettings { get; set; } = default!;

    #endregion

    #region Shared Parameters

    /// <summary>
    /// The unique identifier of the post being displayed.
    /// </summary>
    [Parameter]
    public int PostId { get; set; }

    /// <summary>
    /// The unique identifier of the post author.
    /// </summary>
    [Parameter]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the post author.
    /// </summary>
    [Parameter]
    public string AuthorName { get; set; } = "Anonymous";

    /// <summary>
    /// The URL of the post author's profile image.
    /// </summary>
    [Parameter]
    public string? AuthorImageUrl { get; set; }

    /// <summary>
    /// The friend's user ID when this post was made on their feed (cross-feed post).
    /// </summary>
    [Parameter]
    public string? FriendId { get; set; }

    /// <summary>
    /// The display name of the friend whose feed this post was made on, if applicable.
    /// </summary>
    [Parameter]
    public string? FriendName { get; set; }

    /// <summary>
    /// The username of the friend whose feed this post was made on, if applicable.
    /// </summary>
    [Parameter]
    public string? FriendUserName { get; set; }

    /// <summary>
    /// The profile picture URL of the friend whose feed this post was made on, if applicable.
    /// </summary>
    [Parameter]
    public string? FriendProfilePictureUrl { get; set; }

    /// <summary>
    /// The unique identifier of the current user viewing the modal.
    /// </summary>
    [Parameter]
    public string? CurrentUserId { get; set; }

    /// <summary>
    /// The date and time when the post was created.
    /// </summary>
    [Parameter]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The content of the post article.
    /// </summary>
    [Parameter]
    public RenderFragment? ArticleContent { get; set; }

    /// <summary>
    /// The number of likes on the post.
    /// </summary>
    [Parameter]
    public int LikeCount { get; set; }

    /// <summary>
    /// The total number of comments on the post.
    /// </summary>
    [Parameter]
    public int CommentCount { get; set; }

    /// <summary>
    /// The number of shares of the post.
    /// </summary>
    [Parameter]
    public int ShareCount { get; set; }

    /// <summary>
    /// The initial list of comments (typically not used as modal loads its own).
    /// </summary>
    [Parameter]
    public List<CommentDisplayModel>? Comments { get; set; }

    /// <summary>
    /// The URL of the current user's profile image.
    /// </summary>
    [Parameter]
    public string? CurrentUserImageUrl { get; set; }

    /// <summary>
    /// The display name of the current user.
    /// </summary>
    [Parameter]
    public string? CurrentUserName { get; set; }

    /// <summary>
    /// The breakdown of reaction types and their counts for the post.
    /// </summary>
    [Parameter]
    public Dictionary<LikeType, int>? ReactionBreakdown { get; set; }

    /// <summary>
    /// The current user's reaction type on the post.
    /// </summary>
    [Parameter]
    public LikeType? UserReaction { get; set; }

    /// <summary>
    /// Event callback invoked when the user changes their reaction on the post.
    /// </summary>
    [Parameter]
    public EventCallback<LikeType> OnReactionChanged { get; set; }

    /// <summary>
    /// Event callback invoked when the user removes their reaction from the post.
    /// </summary>
    [Parameter]
    public EventCallback OnRemoveReaction { get; set; }

    /// <summary>
    /// Event callback invoked when a new comment is added to the post.
    /// </summary>
    [Parameter]
    public EventCallback<(int PostId, string Content)> OnCommentAdded { get; set; }

    /// <summary>
    /// Event callback invoked after a comment is fully added (database committed and modal reloaded).
    /// This allows the parent to trigger a refresh of feed list components at the correct time.
    /// The parameter is the PostId that was affected.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnCommentAddedComplete { get; set; }

    /// <summary>
    /// Event callback invoked when a reply is submitted to a comment.
    /// </summary>
    [Parameter]
    public EventCallback<(int ParentCommentId, string Content)> OnReplySubmitted { get; set; }

    /// <summary>
    /// Event callback invoked when a user changes their reaction on a comment.
    /// </summary>
    [Parameter]
    public EventCallback<(int CommentId, LikeType ReactionType)> OnCommentReactionChanged { get; set; }

    /// <summary>
    /// Event callback invoked when a user removes their reaction from a comment.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnRemoveCommentReaction { get; set; }

    /// <summary>
    /// Event callback invoked when a user requests to delete their own comment.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnDeleteComment { get; set; }

    /// <summary>
    /// Event callback invoked after a comment deletion is fully complete.
    /// This allows the parent to trigger a refresh of feed list components at the correct time.
    /// The parameter is the PostId that was affected.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnCommentDeleted { get; set; }

    /// <summary>
    /// Event callback invoked when a user requests to edit their own comment.
    /// </summary>
    [Parameter]
    public EventCallback<(int CommentId, string CurrentContent)> OnEditComment { get; set; }

    /// <summary>
    /// Event callback invoked when a comment is successfully updated with new content via inline editing.
    /// </summary>
    [Parameter]
    public EventCallback<(int CommentId, string NewContent)> OnCommentUpdated { get; set; }

    /// <summary>
    /// Event callback invoked when a user requests to report another user's comment.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnReportComment { get; set; }

    /// <summary>
    /// Event callback invoked when the modal is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    /// Indicates whether the post is pinned.
    /// </summary>
    [Parameter]
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Event callback invoked when the user pins the post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPinPost { get; set; }

    /// <summary>
    /// Event callback invoked when the user unpins the post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnUnpinPost { get; set; }

    /// <summary>
    /// Event callback invoked when the post is deleted.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPostDeleted { get; set; }

    /// <summary>
    /// The list of images attached to the post.
    /// </summary>
    [Parameter]
    public List<PostImage>? Images { get; set; }

    /// <summary>
    /// The optional ID of a specific comment to scroll to and highlight.
    /// </summary>
    [Parameter]
    public int? TargetCommentId { get; set; }

    /// <summary>
    /// Indicates whether the modal is in read-only mode.
    /// </summary>
    [Parameter]
    public bool IsReadOnly { get; set; } = false;

    #endregion

    #region Shared State

    /// <summary>
    /// The list of comments displayed in the modal.
    /// </summary>
    protected List<CommentDisplayModel> modalComments = new();

    /// <summary>
    /// The count of direct (top-level) comments on the post.
    /// </summary>
    protected int directCommentCount = 0;

    /// <summary>
    /// Local copy of the total comment count for display updates.
    /// </summary>
    protected int localCommentCount = 0;

    /// <summary>
    /// Local copy of the like count for display updates.
    /// </summary>
    protected int localLikeCount = 0;

    /// <summary>
    /// The current page number for paginated comment loading.
    /// </summary>
    protected int currentCommentPage = 1;

    /// <summary>
    /// The number of comments to load per page.
    /// </summary>
    protected const int commentPageSize = 20;

    /// <summary>
    /// Indicates whether comments are currently being loaded.
    /// </summary>
    protected bool isLoadingComments = false;

    /// <summary>
    /// Indicates whether additional comments are being loaded.
    /// </summary>
    protected bool isLoadingMoreComments = false;

    /// <summary>
    /// Indicates whether there are more comments available to load.
    /// </summary>
    protected bool hasMoreComments = true;

    /// <summary>
    /// Reference to the modal content element for scroll detection.
    /// </summary>
    protected ElementReference modalContentElement;

    /// <summary>
    /// JavaScript module reference for scroll handling.
    /// </summary>
    protected IJSObjectReference? jsModule;

    /// <summary>
    /// .NET object reference for JavaScript callbacks.
    /// </summary>
    protected DotNetObjectReference<PostDetailModalBase>? dotNetHelper;

    #endregion

    #region Abstract Methods - Service Calls

    /// <summary>
    /// Gets the path to the JavaScript module for this modal type.
    /// </summary>
    /// <returns>The relative path to the JavaScript module.</returns>
    protected abstract string GetJsModulePath();

    /// <summary>
    /// Gets the direct comment count for the post from the appropriate service.
    /// </summary>
    /// <returns>The count of direct (top-level) comments.</returns>
    protected abstract Task<int> GetDirectCommentCountFromServiceAsync();

    /// <summary>
    /// Gets the total comment count for the post from the appropriate service.
    /// Derived classes may return the same value as direct count if total isn't tracked separately.
    /// </summary>
    /// <returns>The total count of all comments including replies.</returns>
    protected virtual Task<int> GetTotalCommentCountFromServiceAsync()
    {
        // Default implementation returns local comment count
        // Override in PostDetailModal to use PostService.GetTotalCommentCountAsync
        return Task.FromResult(localCommentCount);
    }

    /// <summary>
    /// Gets paginated comments for the post from the appropriate service.
    /// </summary>
    /// <param name="pageSize">Number of comments per page.</param>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <returns>A list of comments for the requested page.</returns>
    protected abstract Task<List<CommentDisplayModel>> GetCommentsPagedFromServiceAsync(int pageSize, int pageNumber);

    /// <summary>
    /// Gets the reaction breakdown for a comment from the appropriate service.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>A dictionary of reaction types and their counts.</returns>
    protected abstract Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownFromServiceAsync(int commentId);

    /// <summary>
    /// Gets the like count for a comment from the appropriate service.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>The number of likes on the comment.</returns>
    protected abstract Task<int> GetCommentLikeCountFromServiceAsync(int commentId);

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called when parameters are set. Initializes local counts from parameters.
    /// </summary>
    protected override void OnParametersSet()
    {
        localCommentCount = CommentCount;
        localLikeCount = LikeCount;
    }

    /// <summary>
    /// Called after rendering. Initializes JS interop and loads comments on first render.
    /// </summary>
    /// <param name="firstRender">True if this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load direct comment count
            directCommentCount = await GetDirectCommentCountFromServiceAsync();

            await LoadInitialComments();

            try
            {
                jsModule = await JS.InvokeAsync<IJSObjectReference>("import", GetJsModulePath());
                dotNetHelper = DotNetObjectReference.Create(this);
                await jsModule.InvokeVoidAsync("initializeCommentScroll", modalContentElement, dotNetHelper);

                // If there's a target comment, scroll to it and highlight it
                if (TargetCommentId.HasValue && jsModule != null)
                {
                    await Task.Delay(100); // Small delay to ensure DOM is ready
                    await jsModule.InvokeVoidAsync("scrollToAndHighlightComment", TargetCommentId.Value);
                }
            }
            catch (Exception)
            {
                // Silently fail - scrolling not critical
            }
        }
    }

    #endregion

    #region Comment Loading

    /// <summary>
    /// Loads the initial page of comments.
    /// </summary>
    protected async Task LoadInitialComments()
    {
        isLoadingComments = true;
        StateHasChanged();

        try
        {
            modalComments = await GetCommentsPagedFromServiceAsync(commentPageSize, currentCommentPage);
            hasMoreComments = modalComments.Count == commentPageSize;
        }
        catch (Exception)
        {
            // Silently fail - comments will show empty
        }
        finally
        {
            isLoadingComments = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Loads more comments when scrolling. Called from JavaScript.
    /// </summary>
    [JSInvokable]
    public async Task LoadMoreCommentsAsync()
    {
        if (isLoadingMoreComments || !hasMoreComments) return;

        isLoadingMoreComments = true;
        StateHasChanged();

        try
        {
            currentCommentPage++;
            var newComments = await GetCommentsPagedFromServiceAsync(commentPageSize, currentCommentPage);

            if (newComments.Any())
            {
                modalComments.AddRange(newComments);
                hasMoreComments = newComments.Count == commentPageSize;
            }
            else
            {
                hasMoreComments = false;
            }
        }
        catch (Exception)
        {
            currentCommentPage--;
        }
        finally
        {
            isLoadingMoreComments = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles when a comment is added to the post.
    /// </summary>
    /// <param name="data">The post ID and comment content.</param>
    protected async Task HandleCommentAdded((int PostId, string Content) data)
    {
        // Invoke parent callback
        if (OnCommentAdded.HasDelegate)
        {
            await OnCommentAdded.InvokeAsync(data);
        }

        // Add a delay to ensure database operation completes fully
        await Task.Delay(300);

        // Reload comments to show the new comment
        currentCommentPage = 1;
        hasMoreComments = true;
        await LoadInitialComments();

        // Update comment counts
        directCommentCount = await GetDirectCommentCountFromServiceAsync();
        localCommentCount = await GetTotalCommentCountFromServiceAsync();

        // Notify parent that comment addition is complete and feed should refresh
        if (OnCommentAddedComplete.HasDelegate)
        {
            await OnCommentAddedComplete.InvokeAsync(data.PostId);
        }

        StateHasChanged();
    }

    /// <summary>
    /// Handles when a comment reaction is changed.
    /// </summary>
    /// <param name="data">The comment ID and new reaction type.</param>
    protected async Task HandleCommentReactionChanged((int CommentId, LikeType ReactionType) data)
    {
        // Invoke parent callback
        if (OnCommentReactionChanged.HasDelegate)
        {
            await OnCommentReactionChanged.InvokeAsync(data);
        }

        // Update the local comment's reaction data
        await UpdateLocalCommentReaction(data.CommentId, data.ReactionType);
    }

    /// <summary>
    /// Handles when a comment reaction is removed.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    protected async Task HandleRemoveCommentReaction(int commentId)
    {
        // Invoke parent callback
        if (OnRemoveCommentReaction.HasDelegate)
        {
            await OnRemoveCommentReaction.InvokeAsync(commentId);
        }

        // Update the local comment's reaction data
        await UpdateLocalCommentReaction(commentId, null);
    }

    /// <summary>
    /// Handles when the post is deleted.
    /// </summary>
    /// <param name="postId">The post ID.</param>
    protected async Task HandlePostDeleted(int postId)
    {
        // Invoke parent callback to handle post deletion
        if (OnPostDeleted.HasDelegate)
        {
            await OnPostDeleted.InvokeAsync(postId);
        }

        // Close the modal since the post is being deleted
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles when a reply is submitted to a comment.
    /// </summary>
    /// <param name="data">The parent comment ID and reply content.</param>
    protected async Task HandleReplySubmitted((int ParentCommentId, string Content) data)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId)) return;

        try
        {
            // Notify parent that a reply was submitted so it can handle the database insertion
            if (OnReplySubmitted.HasDelegate)
            {
                await OnReplySubmitted.InvokeAsync((data.ParentCommentId, data.Content));
            }

            // Add a delay to ensure database operation completes fully
            await Task.Delay(300);

            // Reload comments to show the new reply after parent has added it
            currentCommentPage = 1;
            hasMoreComments = true;
            await LoadInitialComments();

            // Update comment counts
            directCommentCount = await GetDirectCommentCountFromServiceAsync();
            localCommentCount = await GetTotalCommentCountFromServiceAsync();

            // Notify parent that comment addition is complete and feed should refresh
            if (OnCommentAddedComplete.HasDelegate)
            {
                await OnCommentAddedComplete.InvokeAsync(PostId);
            }

            StateHasChanged();
        }
        catch (Exception)
        {
            await JS.InvokeVoidAsync("alert", "An error occurred while adding your reply.");
        }
    }

    /// <summary>
    /// Handles when a comment is submitted via the modal footer editor.
    /// </summary>
    /// <param name="commentText">The comment text.</param>
    protected async Task HandleCommentSubmittedInModal(string commentText)
    {
        if (!string.IsNullOrWhiteSpace(commentText))
        {
            await HandleCommentAdded((PostId, commentText));
        }
    }

    /// <summary>
    /// Handles when a comment is deleted, forwarding to parent and refreshing the local comment list.
    /// </summary>
    /// <param name="commentId">The ID of the deleted comment.</param>
    protected async Task HandleDeleteComment(int commentId)
    {
        try
        {
            // Optimistically remove the comment from local list first for immediate UI feedback
            var commentToRemove = CommentHelpers.FindCommentById(modalComments, commentId);
            if (commentToRemove != null)
            {
                // Check if this is a top-level comment (not a reply)
                bool isTopLevelComment = IsTopLevelComment(modalComments, commentId);

                // Remove the comment from the local list immediately
                bool wasRemoved = RemoveCommentFromLocalList(modalComments, commentId);

                if (wasRemoved)
                {
                    // Update comment counts optimistically
                    localCommentCount = Math.Max(0, localCommentCount - 1);
                    if (isTopLevelComment)
                    {
                        directCommentCount = Math.Max(0, directCommentCount - 1);
                    }

                    StateHasChanged(); // Immediate UI update
                }
            }

            // Invoke parent callback to handle database deletion - wait for it to complete
            if (OnDeleteComment.HasDelegate)
            {
                await OnDeleteComment.InvokeAsync(commentId);
            }

            // Add a longer delay to ensure database operation, parent feed update, and re-render complete
            await Task.Delay(500);

            // Reload comments to reflect the deletion and get accurate counts
            currentCommentPage = 1;
            hasMoreComments = true;
            await LoadInitialComments();

            // Update comment counts from service
            directCommentCount = await GetDirectCommentCountFromServiceAsync();
            localCommentCount = await GetTotalCommentCountFromServiceAsync();

            // Notify parent that comment deletion is complete and feed should refresh
            if (OnCommentDeleted.HasDelegate)
            {
                await OnCommentDeleted.InvokeAsync(PostId);
            }

            StateHasChanged();
        }
        catch (Exception)
        {
            // If something goes wrong, reload the comments to get back to a consistent state
            currentCommentPage = 1;
            hasMoreComments = true;
            await LoadInitialComments();

            directCommentCount = await GetDirectCommentCountFromServiceAsync();
            localCommentCount = await GetTotalCommentCountFromServiceAsync();

            StateHasChanged();
        }
    }

    /// <summary>
    /// Checks if a comment is a top-level comment (not a reply).
    /// </summary>
    /// <param name="comments">The list of comments to search.</param>
    /// <param name="commentId">The ID of the comment to check.</param>
    /// <returns>True if the comment is found at the top level, false if it's a reply.</returns>
    private bool IsTopLevelComment(List<CommentDisplayModel> comments, int commentId)
    {
        // Check if the comment is in the top-level list
        return comments.Any(c => c.CommentId == commentId);
    }

    /// <summary>
    /// Removes a comment from the local comment list recursively.
    /// </summary>
    /// <param name="comments">The list of comments to search.</param>
    /// <param name="commentId">The ID of the comment to remove.</param>
    /// <returns>True if the comment was found and removed, false otherwise.</returns>
    private bool RemoveCommentFromLocalList(List<CommentDisplayModel>? comments, int commentId)
    {
        if (comments == null) return false;

        for (int i = comments.Count - 1; i >= 0; i--)
        {
            if (comments[i].CommentId == commentId)
            {
                comments.RemoveAt(i);
                return true; // Found and removed
            }

            // Check replies recursively
            if (comments[i].Replies != null)
            {
                if (RemoveCommentFromLocalList(comments[i].Replies, commentId))
                {
                    return true; // Found and removed in replies
                }
            }
        }

        return false; // Not found
    }

    /// <summary>
    /// Handles when a comment is updated with new content via inline editing.
    /// </summary>
    /// <param name="data">The comment ID and new content.</param>
    protected async Task HandleCommentUpdated((int CommentId, string NewContent) data)
    {
        // Invoke parent callback first
        if (OnCommentUpdated.HasDelegate)
        {
            await OnCommentUpdated.InvokeAsync(data);
        }

        // Update the local comment's text immediately for UI feedback
        var comment = CommentHelpers.FindCommentById(modalComments, data.CommentId);
        if (comment != null)
        {
            comment.CommentText = data.NewContent;
            StateHasChanged();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Updates a local comment's reaction data after a reaction change.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <param name="newReaction">The new reaction type, or null if removed.</param>
    private async Task UpdateLocalCommentReaction(int commentId, LikeType? newReaction)
    {
        // Find the comment in modalComments and update its reaction data
        var comment = CommentHelpers.FindCommentById(modalComments, commentId);
        if (comment != null)
        {
            // Update user's reaction
            comment.UserReaction = newReaction;

            // Reload reaction breakdown and like count
            comment.ReactionBreakdown = await GetCommentReactionBreakdownFromServiceAsync(commentId);
            comment.LikeCount = await GetCommentLikeCountFromServiceAsync(commentId);

            StateHasChanged();
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes of JavaScript resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (jsModule != null)
        {
            try
            {
                await jsModule.InvokeVoidAsync("cleanupCommentScroll");
                await jsModule.DisposeAsync();
            }
            catch { }
        }

        dotNetHelper?.Dispose();
    }

    #endregion
}
