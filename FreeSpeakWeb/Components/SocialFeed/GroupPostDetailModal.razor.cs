using FreeSpeakWeb.Data;
using FreeSpeakWeb.Helpers;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Code-behind for GroupPostDetailModal that provides group-specific service implementations.
/// </summary>
public partial class GroupPostDetailModal
{
    #region Injected Services

    /// <summary>
    /// Service for group post operations.
    /// </summary>
    [Inject]
    private GroupPostService GroupPostService { get; set; } = default!;

    #endregion

    #region Group-Specific Parameters

    /// <summary>
    /// The unique identifier of the group.
    /// </summary>
    [Parameter]
    public int GroupId { get; set; }

    /// <summary>
    /// The name of the group.
    /// </summary>
    [Parameter]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// The author's points in this group.
    /// </summary>
    [Parameter]
    public int? AuthorGroupPoints { get; set; }

    /// <summary>
    /// Indicates whether the points system is enabled for the group.
    /// </summary>
    [Parameter]
    public bool EnablePointsSystem { get; set; } = false;

    /// <summary>
    /// Indicates whether the author is an admin of the group.
    /// </summary>
    [Parameter]
    public bool IsGroupAdmin { get; set; } = false;

    /// <summary>
    /// Indicates whether the author is a moderator of the group.
    /// </summary>
    [Parameter]
    public bool IsGroupModerator { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the current user can delete this post.
    /// For group posts: System Admins, System Moderators, Group Admins, and Group Moderators can delete any post.
    /// Post authors can always delete their own posts.
    /// </summary>
    [Parameter]
    public bool CanDeletePost { get; set; } = false;

    /// <summary>
    /// Event callback invoked when the user requests to edit the post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnEditPost { get; set; }

    #endregion

    #region Overridden Service Methods

    /// <summary>
    /// Gets the path to the JavaScript module for this modal type.
    /// </summary>
    /// <returns>The relative path to the JavaScript module.</returns>
    protected override string GetJsModulePath()
    {
        return "./Components/SocialFeed/GroupPostDetailModal.razor.js";
    }

    /// <summary>
    /// Gets the direct comment count for the group post from GroupPostService.
    /// </summary>
    /// <returns>The count of direct (top-level) comments.</returns>
    protected override async Task<int> GetDirectCommentCountFromServiceAsync()
    {
        return await GroupPostService.GetDirectCommentCountAsync(base.PostId);
    }

    /// <summary>
    /// Gets paginated comments for the group post from GroupPostService.
    /// </summary>
    /// <param name="pageSize">Number of comments per page.</param>
    /// <param name="pageNumber">The page number to retrieve.</param>
    /// <returns>A list of comment display models for the requested page.</returns>
    protected override async Task<List<CommentDisplayModel>> GetCommentsPagedFromServiceAsync(int pageSize, int pageNumber)
    {
        var comments = await GroupPostService.GetCommentsPagedAsync(base.PostId, pageSize, pageNumber);

        // Use batch loading to reduce database round trips
        return await CommentHelpers.BuildCommentDisplayModelsAsync(
            comments.ToList(),
            GroupPostService,
            base.UserPreferenceService,
            base.CurrentUserId);
    }

    /// <summary>
    /// Gets the reaction breakdown for a comment from GroupPostService.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>A dictionary of reaction types and their counts.</returns>
    protected override async Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownFromServiceAsync(int commentId)
    {
        return await GroupPostService.GetCommentReactionBreakdownAsync(commentId);
    }

    /// <summary>
    /// Gets the like count for a comment from GroupPostService.
    /// </summary>
    /// <param name="commentId">The comment ID.</param>
    /// <returns>The number of likes on the comment.</returns>
    protected override async Task<int> GetCommentLikeCountFromServiceAsync(int commentId)
    {
        return await GroupPostService.GetCommentLikeCountAsync(commentId);
    }

    #endregion
}
