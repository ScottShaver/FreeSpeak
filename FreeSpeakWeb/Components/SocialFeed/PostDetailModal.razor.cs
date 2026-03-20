using FreeSpeakWeb.Data;
using FreeSpeakWeb.Helpers;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Code-behind for PostDetailModal that provides feed post-specific service implementations.
/// </summary>
public partial class PostDetailModal
{
    #region Injected Services

    /// <summary>
    /// Service for feed post operations.
    /// </summary>
    [Inject]
    private PostService PostService { get; set; } = default!;

    #endregion

    #region Post-Specific Parameters

    /// <summary>
    /// The audience type (visibility) of the post.
    /// </summary>
    [Parameter]
    public AudienceType AudienceType { get; set; } = AudienceType.Public;

    /// <summary>
    /// Event callback invoked when the post's audience type is changed.
    /// </summary>
    [Parameter]
    public EventCallback<AudienceType> OnAudienceTypeChanged { get; set; }

    #endregion

    #region Local State

    /// <summary>
    /// Total comment count including all nested replies.
    /// </summary>
    private int totalCommentCount = 0;

    #endregion

    #region Overridden Service Methods

    /// <summary>
    /// Gets the path to the JavaScript module for this modal type.
    /// </summary>
    /// <returns>The relative path to the JavaScript module.</returns>
    protected override string GetJsModulePath()
    {
        return "./Components/SocialFeed/PostDetailModal.razor.js";
    }

    /// <summary>
    /// Gets the direct comment count for the post from PostService.
    /// </summary>
    /// <returns>The count of direct (top-level) comments.</returns>
    protected override async Task<int> GetDirectCommentCountFromServiceAsync()
    {
        return await PostService.GetDirectCommentCountAsync(base.PostId);
    }

    /// <summary>
    /// Gets the total comment count for the post from PostService.
    /// </summary>
    /// <returns>The total count of all comments including replies.</returns>
    protected override async Task<int> GetTotalCommentCountFromServiceAsync()
    {
        totalCommentCount = await PostService.GetTotalCommentCountAsync(base.PostId);
        return totalCommentCount;
    }

    /// <summary>
    /// Gets paginated comments for the post from PostService.
    /// </summary>
    /// <param name="pageSize">Number of comments per page.</param>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <returns>A list of comment display models for the requested page.</returns>
    protected override async Task<List<CommentDisplayModel>> GetCommentsPagedFromServiceAsync(int pageSize, int pageNumber)
    {
        var comments = await PostService.GetCommentsPagedAsync(base.PostId, pageSize, pageNumber);

        // Use batch loading to reduce database round trips
        return await CommentHelpers.BuildCommentDisplayModelsAsync(
            comments.ToList(),
            PostService,
            base.UserPreferenceService,
            base.CurrentUserId);
    }

    /// <summary>
    /// Gets the reaction breakdown for a comment from PostService.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>A dictionary of reaction types and their counts.</returns>
    protected override async Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownFromServiceAsync(int commentId)
    {
        return await PostService.GetCommentReactionBreakdownAsync(commentId);
    }

    /// <summary>
    /// Gets the like count for a comment from PostService.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>The number of likes on the comment.</returns>
    protected override async Task<int> GetCommentLikeCountFromServiceAsync(int commentId)
    {
        return await PostService.GetCommentLikeCountAsync(commentId);
    }

    #endregion

    #region Lifecycle Overrides

    /// <summary>
    /// Called after rendering. Loads total comment count in addition to base behavior.
    /// </summary>
    /// <param name="firstRender">True if this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load total comment count for feed posts
            totalCommentCount = await PostService.GetTotalCommentCountAsync(base.PostId);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
}
