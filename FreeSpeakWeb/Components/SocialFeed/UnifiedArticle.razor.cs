using FreeSpeakWeb.Components.Shared;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Helpers;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Unified article component that handles both UserPost and GroupPost types.
/// Uses composition to delegate to appropriate services based on the PostType parameter.
/// </summary>
public partial class UnifiedArticle : ArticleComponentBase
{
    #region Injected Services

    [Inject]
    private PostService PostService { get; set; } = default!;

    [Inject]
    private GroupPostService GroupPostService { get; set; } = default!;

    [Inject]
    private IOptions<SiteSettings> SiteSettings { get; set; } = default!;

    [Inject]
    private UserPreferenceService UserPreferenceService { get; set; } = default!;

    [Inject]
    private IAuditLogRepository AuditLogRepository { get; set; } = default!;

    [Inject]
    private IStringLocalizer<FreeSpeakWeb.Resources.SocialFeed.FeedArticle> Localizer { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// Gets or sets the type of post (UserPost or GroupPost).
    /// </summary>
    [Parameter]
    public PostType ArticlePostType { get; set; } = PostType.UserPost;

    /// <summary>
    /// Gets or sets the audience type for the post (UserPost only).
    /// </summary>
    [Parameter]
    public AudienceType AudienceType { get; set; } = AudienceType.Public;

    /// <summary>
    /// Event callback invoked when the post's audience type is changed (UserPost only).
    /// </summary>
    [Parameter]
    public EventCallback<AudienceType> OnAudienceTypeChanged { get; set; }

    /// <summary>
    /// Gets or sets the group ID this post belongs to (GroupPost only).
    /// </summary>
    [Parameter]
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the group name for display (GroupPost only).
    /// </summary>
    [Parameter]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author's points in this group (GroupPost only).
    /// </summary>
    [Parameter]
    public int? AuthorGroupPoints { get; set; }

    #endregion

    #region State Fields

    // Audience type state (UserPost only)
    private AudienceType currentAudienceType = AudienceType.Public;
    private bool hasLocalAudienceChange = false;
    private bool showAudienceMenu = false;
    private bool showPublicConfirmation = false;
    private AudienceType pendingAudienceType = AudienceType.FriendsOnly;
    private List<PopupMenu.PopupMenuItem> audienceMenuItems = new();
    private string audienceMenuPositionStyle = string.Empty;

    // Report modal state (GroupPost only)
    private bool showReportModal = false;

    // Total comment count for internal loading (UserPost-specific)
    private int internalTotalCommentCount = 0;

    // DotNetObjectReference for JS interop
    private DotNetObjectReference<UnifiedArticle>? dotNetHelper;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets whether the comment limit has been reached for this post.
    /// </summary>
    private bool HasReachedCommentLimit => DirectCommentCount >= SiteSettings.Value.MaxFeedPostDirectCommentCount;

    /// <summary>
    /// Gets the delete confirmation message based on post type.
    /// </summary>
    private string DeleteConfirmationMessage => ArticlePostType switch
    {
        PostType.GroupPost => "Are you sure you want to delete this group post? This action cannot be undone and will permanently remove the post, all comments, likes, and images.",
        _ => "Are you sure you want to delete this post? This action cannot be undone and will permanently remove the post, all comments, likes, and images."
    };

    #endregion

    #region Menu Building (Override)

    /// <summary>
    /// Builds the menu items for the post context menu based on post type.
    /// </summary>
    protected override void BuildMenuItems()
    {
        menuItems.Clear();

        if (ArticlePostType == PostType.GroupPost)
        {
            BuildGroupPostMenuItems();
        }
        else
        {
            BuildUserPostMenuItems();
        }
    }

    /// <summary>
    /// Builds menu items for GroupPost type.
    /// </summary>
    private void BuildGroupPostMenuItems()
    {
        if (IsOwnPost)
        {
            if (IsModalView)
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                    Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["CopyLink"],
                    Icon = "bi bi-link-45deg",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                });
            }
            else
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["EditPost"],
                    Icon = "bi bi-pencil",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleEditPostClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                    Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = isNotificationMuted ? Localizer["TurnOnNotifications"] : Localizer["TurnOffNotifications"],
                    Icon = isNotificationMuted ? "bi bi-bell" : "bi bi-bell-slash",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleToggleNotificationsClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["DeletePost"],
                    Icon = "bi bi-trash",
                    OnClick = EventCallback.Factory.Create(this, ShowDeleteConfirmation)
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["CopyLink"],
                    Icon = "bi bi-link-45deg",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                });
            }
        }
        else if (!string.IsNullOrEmpty(CurrentUserId))
        {
            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
            });

            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = isNotificationMuted ? Localizer["TurnOnNotifications"] : Localizer["TurnOffNotifications"],
                Icon = isNotificationMuted ? "bi bi-bell" : "bi bi-bell-slash",
                OnClick = EventCallback.Factory.Create(this, async () => await HandleToggleNotificationsClick())
            });

            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = Localizer["ReportPost"],
                Icon = "bi bi-flag",
                OnClick = EventCallback.Factory.Create(this, () => ShowReportModal())
            });

            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = Localizer["CopyLink"],
                Icon = "bi bi-link-45deg",
                OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
            });
        }
        else
        {
            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = Localizer["CopyLink"],
                Icon = "bi bi-link-45deg",
                OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
            });
        }
    }

    /// <summary>
    /// Builds menu items for UserPost type.
    /// </summary>
    private void BuildUserPostMenuItems()
    {
        if (IsOwnPost)
        {
            if (IsModalView)
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                    Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
                });

                if (currentAudienceType == AudienceType.Public)
                {
                    menuItems.Add(new PopupMenu.PopupMenuItem
                    {
                        Text = Localizer["CopyLink"],
                        Icon = "bi bi-link-45deg",
                        OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                    });
                }
            }
            else
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["EditPost"],
                    Icon = "bi bi-pencil",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleEditPostClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                    Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["ChangePostAudience"],
                    Icon = "bi bi-globe",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleChangeAudienceClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = isNotificationMuted ? Localizer["TurnOnNotifications"] : Localizer["TurnOffNotifications"],
                    Icon = isNotificationMuted ? "bi bi-bell" : "bi bi-bell-slash",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleToggleNotificationsClick())
                });

                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["DeletePost"],
                    Icon = "bi bi-trash",
                    OnClick = EventCallback.Factory.Create(this, ShowDeleteConfirmation)
                });

                if (currentAudienceType == AudienceType.Public)
                {
                    menuItems.Add(new PopupMenu.PopupMenuItem
                    {
                        Text = Localizer["CopyLink"],
                        Icon = "bi bi-link-45deg",
                        OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                    });
                }
            }
        }
        else if (!string.IsNullOrEmpty(CurrentUserId))
        {
            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = currentIsPinned ? Localizer["UnpinPost"] : Localizer["PinPost"],
                Icon = currentIsPinned ? "bi bi-pin-angle-fill" : "bi bi-pin-angle",
                OnClick = EventCallback.Factory.Create(this, async () => await HandlePinUnpinClick())
            });

            // TODO: Implement feed post reporting
            menuItems.Add(new PopupMenu.PopupMenuItem
            {
                Text = Localizer["ReportPost"],
                Icon = "bi bi-flag",
                OnClick = EventCallback.Factory.Create(this, async () => await JS.InvokeVoidAsync("alert", "Report Post - Coming Soon"))
            });

            if (currentAudienceType == AudienceType.Public)
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["CopyLink"],
                    Icon = "bi bi-link-45deg",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                });
            }
        }
        else
        {
            if (currentAudienceType == AudienceType.Public)
            {
                menuItems.Add(new PopupMenu.PopupMenuItem
                {
                    Text = Localizer["CopyLink"],
                    Icon = "bi bi-link-45deg",
                    OnClick = EventCallback.Factory.Create(this, async () => await HandleCopyLink())
                });
            }
        }
    }

    #endregion

    #region Report Modal Methods (GroupPost only)

    /// <summary>
    /// Shows the report post modal.
    /// </summary>
    private void ShowReportModal()
    {
        CloseMenu();
        showReportModal = true;
        StateHasChanged();
    }

    /// <summary>
    /// Closes the report post modal.
    /// </summary>
    private void CloseReportModal()
    {
        showReportModal = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles when a report is successfully submitted.
    /// </summary>
    private Task HandleReportSubmitted()
    {
        AlertService.ShowSuccess("Report submitted successfully. Thank you for helping keep our community safe.");
        return Task.CompletedTask;
    }

    #endregion

    #region Audience Menu Methods (UserPost only)

    /// <summary>
    /// Builds the audience selection menu items.
    /// </summary>
    private void BuildAudienceMenuItems()
    {
        audienceMenuItems.Clear();

        audienceMenuItems.Add(new PopupMenu.PopupMenuItem
        {
            Text = Localizer["Public"],
            Icon = "bi bi-globe",
            OnClick = EventCallback.Factory.Create(this, () => SelectAudienceType(AudienceType.Public))
        });

        audienceMenuItems.Add(new PopupMenu.PopupMenuItem
        {
            Text = Localizer["FriendsOnly"],
            Icon = "bi bi-people",
            OnClick = EventCallback.Factory.Create(this, () => SelectAudienceType(AudienceType.FriendsOnly))
        });

        audienceMenuItems.Add(new PopupMenu.PopupMenuItem
        {
            Text = Localizer["OnlyMe"],
            Icon = "bi bi-lock",
            OnClick = EventCallback.Factory.Create(this, () => SelectAudienceType(AudienceType.MeOnly))
        });
    }

    /// <summary>
    /// Handles clicking the change audience menu item.
    /// </summary>
    private Task HandleChangeAudienceClick()
    {
        CloseMenu();
        BuildAudienceMenuItems();
        showAudienceMenu = true;
        audienceMenuPositionStyle = "right: 0; top: 40px;";
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles selecting an audience type from the menu.
    /// </summary>
    /// <param name="newAudienceType">The selected audience type.</param>
    private async Task SelectAudienceType(AudienceType newAudienceType)
    {
        CloseAudienceMenu();

        if (currentAudienceType == newAudienceType)
        {
            return;
        }

        if (newAudienceType == AudienceType.Public)
        {
            pendingAudienceType = newAudienceType;
            showPublicConfirmation = true;
            StateHasChanged();
            return;
        }

        await ApplyAudienceChange(newAudienceType);
    }

    /// <summary>
    /// Confirms changing the post to public audience.
    /// </summary>
    private async Task ConfirmPublicAudience()
    {
        showPublicConfirmation = false;
        StateHasChanged();
        await ApplyAudienceChange(pendingAudienceType);
    }

    /// <summary>
    /// Cancels the public audience confirmation.
    /// </summary>
    private void CancelPublicConfirmation()
    {
        showPublicConfirmation = false;
        pendingAudienceType = currentAudienceType;
        StateHasChanged();
    }

    /// <summary>
    /// Applies the audience change to the post.
    /// </summary>
    /// <param name="newAudienceType">The new audience type.</param>
    private async Task ApplyAudienceChange(AudienceType newAudienceType)
    {
        var result = await PostService.UpdatePostAudienceAsync(PostId, AuthorId, newAudienceType);

        if (result.Success)
        {
            currentAudienceType = newAudienceType;
            hasLocalAudienceChange = true;

            if (currentIsPinned)
            {
                currentIsPinned = false;
                hasLocalPinnedChange = true;

                if (OnUnpinPost.HasDelegate)
                {
                    await OnUnpinPost.InvokeAsync(PostId);
                }
            }

            if (OnAudienceTypeChanged.HasDelegate)
            {
                await OnAudienceTypeChanged.InvokeAsync(newAudienceType);
            }

            try
            {
                await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserPost, new UserPostDetails
                {
                    PostId = PostId,
                    Visibility = newAudienceType.ToString(),
                    OperationType = OperationTypeEnum.Update.ToString()
                });
            }
            catch { /* Audit logging should not fail the operation */ }

            StateHasChanged();
        }
        else
        {
            AlertService.ShowError(result.ErrorMessage ?? "Failed to update post audience.");
        }
    }

    /// <summary>
    /// Closes the audience selection menu.
    /// </summary>
    private void CloseAudienceMenu()
    {
        showAudienceMenu = false;
        StateHasChanged();
    }

    #endregion

    #region Post Actions

    /// <summary>
    /// Confirms and executes post deletion based on post type.
    /// </summary>
    private async Task ConfirmDeletePost()
    {
        showDeleteConfirmation = false;
        StateHasChanged();

        if (ArticlePostType == PostType.GroupPost)
        {
            await DeleteGroupPost();
        }
        else
        {
            await DeleteUserPost();
        }
    }

    /// <summary>
    /// Deletes a user post.
    /// </summary>
    private async Task DeleteUserPost()
    {
        var result = await PostService.DeletePostAsync(PostId, AuthorId);

        if (result.Success)
        {
            try
            {
                await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserPost, new UserPostDetails
                {
                    PostId = PostId,
                    OperationType = OperationTypeEnum.Delete.ToString()
                });
            }
            catch { /* Audit logging should not fail the operation */ }

            if (OnPostDeleted.HasDelegate)
            {
                await OnPostDeleted.InvokeAsync(PostId);
            }
        }
        else
        {
            AlertService.ShowError(result.ErrorMessage ?? "Failed to delete post.");
        }
    }

    /// <summary>
    /// Deletes a group post.
    /// </summary>
    private async Task DeleteGroupPost()
    {
        var result = await GroupPostService.DeleteGroupPostAsync(PostId, AuthorId);

        if (result.Success)
        {
            try
            {
                await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserGroupPost, new UserGroupPostDetails
                {
                    PostId = PostId,
                    GroupId = GroupId,
                    OperationType = OperationTypeEnum.Delete.ToString()
                });
            }
            catch { /* Audit logging should not fail the operation */ }

            if (OnPostDeleted.HasDelegate)
            {
                await OnPostDeleted.InvokeAsync(PostId);
            }
        }
        else
        {
            AlertService.ShowError(result.ErrorMessage ?? "Failed to delete post.");
        }
    }

    /// <summary>
    /// Copies the post link to clipboard based on post type.
    /// </summary>
    private async Task HandleCopyLink()
    {
        CloseMenu();

        try
        {
            var baseUri = NavigationManager.BaseUri.TrimEnd('/');
            var postUrl = ArticlePostType == PostType.GroupPost
                ? $"{baseUri}/group/{GroupId}/post/{PostId}"
                : $"{baseUri}/post/{PostId}";

            await JS.InvokeVoidAsync("navigator.clipboard.writeText", postUrl);

            try
            {
                if (ArticlePostType == PostType.GroupPost)
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserGroupPost, new UserGroupPostDetails
                    {
                        PostId = PostId,
                        GroupId = GroupId,
                        OperationType = OperationTypeEnum.CopyLink.ToString()
                    });
                }
                else
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserPost, new UserPostDetails
                    {
                        PostId = PostId,
                        OperationType = OperationTypeEnum.CopyLink.ToString()
                    });
                }
            }
            catch { /* Audit logging should not fail the operation */ }
        }
        catch (Exception)
        {
            AlertService.ShowError("Failed to copy link to clipboard.");
        }
    }

    /// <summary>
    /// Toggles notification mute status for the post based on post type.
    /// </summary>
    private async Task HandleToggleNotificationsClick()
    {
        CloseMenu();

        if (string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            if (ArticlePostType == PostType.GroupPost)
            {
                await ToggleGroupPostNotifications();
            }
            else
            {
                await ToggleUserPostNotifications();
            }
        }
        catch (Exception)
        {
            AlertService.ShowError("Failed to update notification settings.");
        }
    }

    /// <summary>
    /// Toggles notifications for a user post.
    /// </summary>
    private async Task ToggleUserPostNotifications()
    {
        if (isNotificationMuted)
        {
            var result = await PostService.UnmutePostNotificationsAsync(PostId, CurrentUserId!);
            if (result.Success)
            {
                isNotificationMuted = false;

                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.UnmutePost.ToString(),
                        ContentSummary = $"Unmuted notifications for post {PostId}"
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
        }
        else
        {
            var result = await PostService.MutePostNotificationsAsync(PostId, CurrentUserId!);
            if (result.Success)
            {
                isNotificationMuted = true;

                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.MutePost.ToString(),
                        ContentSummary = $"Muted notifications for post {PostId}"
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Toggles notifications for a group post.
    /// </summary>
    private async Task ToggleGroupPostNotifications()
    {
        if (isNotificationMuted)
        {
            var result = await GroupPostService.UnmutePostNotificationsAsync(PostId, CurrentUserId!);
            if (result.Success)
            {
                isNotificationMuted = false;

                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.UnmuteGroupPost.ToString(),
                        ContentSummary = $"Unmuted notifications for group post {PostId} in group {GroupId}"
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
        }
        else
        {
            var result = await GroupPostService.MutePostNotificationsAsync(PostId, CurrentUserId!);
            if (result.Success)
            {
                isNotificationMuted = true;

                try
                {
                    await AuditLogRepository.LogActionAsync(CurrentUserId, ActionCategory.UserNotification, new UserNotificationDetails
                    {
                        OperationType = OperationTypeEnum.MuteGroupPost.ToString(),
                        ContentSummary = $"Muted notifications for group post {PostId} in group {GroupId}"
                    });
                }
                catch { /* Audit logging should not fail the operation */ }

                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Shows the likes modal with reaction details based on post type.
    /// </summary>
    private async Task ShowLikesModal()
    {
        if (LikeCount == 0) return;

        showLikesModal = true;
        isLoadingLikes = true;
        StateHasChanged();

        try
        {
            likes = ArticlePostType == PostType.GroupPost
                ? await GroupPostService.GetGroupPostLikesWithDetailsAsync(PostId)
                : await PostService.GetPostLikesWithDetailsAsync(PostId);
        }
        catch (Exception)
        {
            // Silently fail - likes list optional
        }
        finally
        {
            isLoadingLikes = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Comment Operations

    /// <summary>
    /// Handles comment submission.
    /// </summary>
    /// <param name="commentText">The comment text.</param>
    private async Task OnCommentSubmitted(string commentText)
    {
        if (OnCommentAdded.HasDelegate && !string.IsNullOrWhiteSpace(commentText))
        {
            await OnCommentAdded.InvokeAsync((PostId, commentText));

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Loads comments from the database based on post type.
    /// </summary>
    protected override async Task LoadCommentsAsync()
    {
        if (isLoadingComments) return;
        isLoadingComments = true;

        try
        {
            if (ArticlePostType == PostType.GroupPost)
            {
                await LoadGroupPostCommentsAsync();
            }
            else
            {
                await LoadUserPostCommentsAsync();
            }
        }
        catch (Exception)
        {
            // Silently fail - comments optional
        }
        finally
        {
            isLoadingComments = false;
        }
    }

    /// <summary>
    /// Loads comments for a user post.
    /// </summary>
    private async Task LoadUserPostCommentsAsync()
    {
        var allComments = await PostService.GetCommentsAsync(PostId);

        var topComments = allComments
            .OrderByDescending(c => c.CreatedAt)
            .Take(CommentsToShow)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        internalComments = await CommentHelpers.BuildCommentDisplayModelsAsync(
            topComments,
            PostService,
            UserPreferenceService,
            CurrentUserId);
        internalDirectCommentCount = allComments.Count(c => c.ParentCommentId == null);
        internalTotalCommentCount = await PostService.GetTotalCommentCountAsync(PostId);
    }

    /// <summary>
    /// Loads comments for a group post.
    /// </summary>
    private async Task LoadGroupPostCommentsAsync()
    {
        var allComments = await GroupPostService.GetCommentsAsync(PostId);

        var topComments = allComments
            .OrderByDescending(c => c.CreatedAt)
            .Take(CommentsToShow)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        internalComments = await BuildGroupPostCommentModelsAsync(topComments);
        internalDirectCommentCount = allComments.Count(c => c.ParentCommentId == null);
    }

    /// <summary>
    /// Builds comment display models from group post comment entities.
    /// </summary>
    /// <param name="comments">The comments to build models for.</param>
    /// <returns>A list of comment display models.</returns>
    private async Task<List<CommentDisplayModel>> BuildGroupPostCommentModelsAsync(List<GroupPostComment> comments)
    {
        var models = new List<CommentDisplayModel>();
        foreach (var c in comments)
        {
            models.Add(await BuildGroupPostCommentModelAsync(c));
        }
        return models;
    }

    /// <summary>
    /// Builds a single comment display model from a group post comment entity.
    /// </summary>
    /// <param name="comment">The comment to build a model for.</param>
    /// <returns>A comment display model.</returns>
    private async Task<CommentDisplayModel> BuildGroupPostCommentModelAsync(GroupPostComment comment)
    {
        var replies = await GroupPostService.GetRepliesAsync(comment.Id);
        var replyModels = new List<CommentDisplayModel>();

        foreach (var reply in replies)
        {
            var replyModel = await BuildGroupPostCommentModelAsync(reply);
            replyModels.Add(replyModel);
        }

        var userName = "Unknown";
        if (comment.Author != null)
        {
            userName = await UserPreferenceService.FormatUserDisplayNameAsync(
                comment.Author.Id,
                comment.Author.FirstName,
                comment.Author.LastName,
                comment.Author.UserName ?? "Unknown"
            );
        }

        LikeType? userReaction = null;
        if (!string.IsNullOrEmpty(CurrentUserId))
        {
            var userLike = await GroupPostService.GetUserCommentLikeAsync(comment.Id, CurrentUserId);
            userReaction = userLike?.Type;
        }

        return new CommentDisplayModel
        {
            CommentId = comment.Id,
            UserName = userName,
            UserImageUrl = comment.Author?.ProfilePictureUrl,
            CommentAuthorId = comment.AuthorId,
            CommentText = comment.Content,
            ImageUrl = comment.ImageUrl,
            Timestamp = comment.CreatedAt,
            Replies = replyModels.Any() ? replyModels : null,
            LikeCount = 0,
            UserReaction = userReaction,
            ReactionBreakdown = new Dictionary<LikeType, int>()
        };
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called when parameters are set. Handles state synchronization.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        // Sync audience type state (UserPost only)
        if (ArticlePostType == PostType.UserPost)
        {
            if (!hasLocalAudienceChange)
            {
                currentAudienceType = AudienceType;
            }
            else if (currentAudienceType == AudienceType)
            {
                hasLocalAudienceChange = false;
            }
        }

        // Sync pinned state
        if (!hasLocalPinnedChange)
        {
            currentIsPinned = IsPinned;
        }
        else if (currentIsPinned == IsPinned)
        {
            hasLocalPinnedChange = false;
        }

        // Use IsMuted parameter if provided
        if (IsMuted.HasValue)
        {
            isNotificationMuted = IsMuted.Value;
            hasMuteStatusFromParameter = true;
        }

        // Load comments if internal loading enabled
        if (LoadCommentsInternally && !isLoadingComments && (lastLoadedPostId != PostId || lastRefreshTrigger != RefreshTrigger))
        {
            lastLoadedPostId = PostId;
            lastRefreshTrigger = RefreshTrigger;
            await LoadCommentsAsync();
        }
    }

    /// <summary>
    /// Called after the component renders. Initializes JS interop.
    /// </summary>
    /// <param name="firstRender">Whether this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load mute status if not provided by parent
            if (!hasMuteStatusFromParameter && !string.IsNullOrEmpty(CurrentUserId))
            {
                try
                {
                    isNotificationMuted = ArticlePostType == PostType.GroupPost
                        ? await GroupPostService.IsPostNotificationMutedAsync(PostId, CurrentUserId)
                        : await PostService.IsPostNotificationMutedAsync(PostId, CurrentUserId);
                    StateHasChanged();
                }
                catch (Exception)
                {
                    // Silently fail - mute status optional
                }
            }

            // Initialize content measurement JS
            if (!IsModalView)
            {
                try
                {
                    module = await JS.InvokeAsync<IJSObjectReference>("import", "./Components/SocialFeed/FeedArticle.razor.js");
                    dotNetHelper = DotNetObjectReference.Create(this);
                    await module.InvokeVoidAsync("initializeContentMeasurement", contentElement, dotNetHelper);
                }
                catch (Exception)
                {
                    // Silently fail - content measurement optional
                }
            }
        }
    }

    /// <summary>
    /// Disposes of managed resources asynchronously.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        dotNetHelper?.Dispose();
    }

    #endregion
}
