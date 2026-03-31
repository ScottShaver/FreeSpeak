using FreeSpeakWeb.Components.Shared;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Base class for article components (FeedArticle and GroupPostArticle) containing shared state and methods.
/// This eliminates code duplication for common UI behaviors like reaction pickers, image viewers, 
/// likes modals, menu handling, and comment operations.
/// </summary>
public abstract class ArticleComponentBase : ComponentBase, IAsyncDisposable
{
    #region Injected Services

    [Inject]
    protected IJSRuntime JS { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected AlertService AlertService { get; set; } = default!;

    #endregion

    #region Common Parameters

    /// <summary>
    /// Gets or sets the post ID.
    /// </summary>
    [Parameter]
    public int PostId { get; set; }

    /// <summary>
    /// Gets or sets the author's user ID.
    /// </summary>
    [Parameter]
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author's display name.
    /// </summary>
    [Parameter]
    public string AuthorName { get; set; } = "Anonymous";

    /// <summary>
    /// Gets or sets the author's profile image URL.
    /// </summary>
    [Parameter]
    public string? AuthorImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the current user's ID.
    /// </summary>
    [Parameter]
    public string? CurrentUserId { get; set; }

    /// <summary>
    /// Gets or sets when the post was created.
    /// </summary>
    [Parameter]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the article content as a render fragment.
    /// </summary>
    [Parameter]
    public RenderFragment? ArticleContent { get; set; }

    /// <summary>
    /// Gets or sets the number of likes on the post.
    /// </summary>
    [Parameter]
    public int LikeCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of comments on the post.
    /// </summary>
    [Parameter]
    public int CommentCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of shares on the post.
    /// </summary>
    [Parameter]
    public int ShareCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of direct (non-reply) comments on the post.
    /// </summary>
    [Parameter]
    public int DirectCommentCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the list of comments to display.
    /// </summary>
    [Parameter]
    public List<CommentDisplayModel>? Comments { get; set; }

    /// <summary>
    /// Gets or sets the current user's profile image URL.
    /// </summary>
    [Parameter]
    public string? CurrentUserImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the current user's display name.
    /// </summary>
    [Parameter]
    public string? CurrentUserName { get; set; }

    /// <summary>
    /// Gets or sets the breakdown of reactions by type.
    /// </summary>
    [Parameter]
    public Dictionary<LikeType, int>? ReactionBreakdown { get; set; }

    /// <summary>
    /// Gets or sets the current user's reaction to this post.
    /// </summary>
    [Parameter]
    public LikeType? UserReaction { get; set; }

    /// <summary>
    /// Event callback invoked when the user's reaction changes.
    /// </summary>
    [Parameter]
    public EventCallback<LikeType> OnReactionChanged { get; set; }

    /// <summary>
    /// Event callback invoked when the user removes their reaction.
    /// </summary>
    [Parameter]
    public EventCallback OnRemoveReaction { get; set; }

    /// <summary>
    /// Event callback invoked when a new comment is added.
    /// </summary>
    [Parameter]
    public EventCallback<(int PostId, string Content)> OnCommentAdded { get; set; }

    /// <summary>
    /// Event callback invoked when a reply is submitted.
    /// </summary>
    [Parameter]
    public EventCallback<(int ParentCommentId, string Content)> OnReplySubmitted { get; set; }

    /// <summary>
    /// Event callback invoked when a comment reaction changes.
    /// </summary>
    [Parameter]
    public EventCallback<(int CommentId, LikeType ReactionType)> OnCommentReactionChanged { get; set; }

    /// <summary>
    /// Event callback invoked when a comment reaction is removed.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnRemoveCommentReaction { get; set; }

    /// <summary>
    /// Event callback invoked when a user requests to delete their own comment.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnDeleteComment { get; set; }

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
    /// Gets or sets whether to hide the comment editor.
    /// </summary>
    [Parameter]
    public bool HideCommentEditor { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this component is displayed in a modal view.
    /// </summary>
    [Parameter]
    public bool IsModalView { get; set; } = false;

    /// <summary>
    /// Event callback invoked when the post detail should be shown.
    /// </summary>
    [Parameter]
    public EventCallback OnShowPostDetail { get; set; }

    /// <summary>
    /// Event callback invoked when comments should be shown.
    /// </summary>
    [Parameter]
    public EventCallback OnShowComments { get; set; }

    /// <summary>
    /// Gets or sets whether this post is pinned.
    /// </summary>
    [Parameter]
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Event callback invoked when the user pins a post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPinPost { get; set; }

    /// <summary>
    /// Event callback invoked when the user unpins a post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnUnpinPost { get; set; }

    /// <summary>
    /// Event callback invoked when a post is deleted.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPostDeleted { get; set; }

    /// <summary>
    /// Event callback invoked when the user wants to edit a post.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnEditPost { get; set; }

    /// <summary>
    /// Gets or sets the list of images attached to this post.
    /// </summary>
    [Parameter]
    public List<PostImage>? Images { get; set; }

    /// <summary>
    /// Gets or sets whether this component is in read-only mode.
    /// </summary>
    [Parameter]
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets whether comments should be loaded internally by the component.
    /// </summary>
    [Parameter]
    public bool LoadCommentsInternally { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of comments to show when loading internally.
    /// </summary>
    [Parameter]
    public int CommentsToShow { get; set; } = 3;

    /// <summary>
    /// Gets or sets the refresh trigger to force comment reload.
    /// </summary>
    [Parameter]
    public int RefreshTrigger { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether notifications are muted for this post.
    /// When provided by parent, avoids N+1 queries by using batch-loaded mute status.
    /// </summary>
    [Parameter]
    public bool? IsMuted { get; set; }

    #endregion

    #region Shared State Fields

    /// <summary>
    /// Whether the reaction picker is currently shown.
    /// </summary>
    protected bool showReactionPicker = false;

    /// <summary>
    /// Timer used to hide the reaction picker after mouse leave.
    /// </summary>
    protected System.Threading.Timer? hideTimer;

    /// <summary>
    /// Whether the likes modal is currently shown.
    /// </summary>
    protected bool showLikesModal = false;

    /// <summary>
    /// Whether likes are currently being loaded.
    /// </summary>
    protected bool isLoadingLikes = false;

    /// <summary>
    /// List of likes with user details for the likes modal.
    /// </summary>
    protected List<(ApplicationUser User, LikeType Type, DateTime CreatedAt)> likes = new();

    /// <summary>
    /// Reference to the content element for measuring truncation.
    /// </summary>
    protected ElementReference contentElement;

    /// <summary>
    /// Reference to the JavaScript module.
    /// </summary>
    protected IJSObjectReference? module;

    /// <summary>
    /// Whether the content is truncated.
    /// </summary>
    protected bool isTruncated = false;

    /// <summary>
    /// Whether the expand button should be shown.
    /// </summary>
    protected bool shouldShowExpandButton = false;

    /// <summary>
    /// Whether the post menu is currently shown.
    /// </summary>
    protected bool showMenu = false;

    /// <summary>
    /// Menu items for the post menu.
    /// </summary>
    protected List<PopupMenu.PopupMenuItem> menuItems = new();

    /// <summary>
    /// CSS position style for the menu.
    /// </summary>
    protected string menuPositionStyle = string.Empty;

    /// <summary>
    /// Whether the delete confirmation modal is shown.
    /// </summary>
    protected bool showDeleteConfirmation = false;

    /// <summary>
    /// Whether the image viewer modal is shown.
    /// </summary>
    protected bool showImageViewer = false;

    /// <summary>
    /// URLs for images in the image viewer.
    /// </summary>
    protected List<string> imageUrls = new();

    /// <summary>
    /// Index of the currently selected image in the viewer.
    /// </summary>
    protected int selectedImageIndex = 0;

    /// <summary>
    /// Internal list of comments when loading internally.
    /// </summary>
    protected List<CommentDisplayModel> internalComments = new();

    /// <summary>
    /// Count of direct comments when loading internally.
    /// </summary>
    protected int internalDirectCommentCount = 0;

    /// <summary>
    /// Whether comments are currently being loaded.
    /// </summary>
    protected bool isLoadingComments = false;

    /// <summary>
    /// Last loaded post ID for caching purposes.
    /// </summary>
    protected int? lastLoadedPostId = null;

    /// <summary>
    /// Last refresh trigger value for detecting changes.
    /// </summary>
    protected int lastRefreshTrigger = 0;

    /// <summary>
    /// Whether notifications are muted (internal state).
    /// </summary>
    protected bool isNotificationMuted = false;

    /// <summary>
    /// Whether mute status was provided by parameter.
    /// </summary>
    protected bool hasMuteStatusFromParameter = false;

    /// <summary>
    /// Current pinned state (internal, may differ from parameter during local changes).
    /// </summary>
    protected bool currentIsPinned = false;

    /// <summary>
    /// Whether we have a local pinned change that parent hasn't reflected yet.
    /// </summary>
    protected bool hasLocalPinnedChange = false;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets whether the current user is the author of this post.
    /// </summary>
    protected bool IsOwnPost => !string.IsNullOrEmpty(CurrentUserId) && !string.IsNullOrEmpty(AuthorId) && CurrentUserId == AuthorId;

    #endregion

    #region Reaction Picker Methods

    /// <summary>
    /// Shows the reaction picker (for non-own posts).
    /// </summary>
    protected void ShowReactionPicker()
    {
        if (IsOwnPost) return;

        hideTimer?.Dispose();
        if (!showReactionPicker)
        {
            showReactionPicker = true;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Cancels the hide timer and keeps the reaction picker visible.
    /// </summary>
    protected void CancelHideTimer()
    {
        if (IsOwnPost) return;

        hideTimer?.Dispose();
        // Don't call StateHasChanged if already showing - prevents render loop
    }

    /// <summary>
    /// Starts a timer to hide the reaction picker after 500ms.
    /// </summary>
    protected void StartHideTimer()
    {
        hideTimer?.Dispose();
        hideTimer = new System.Threading.Timer(_ =>
        {
            InvokeAsync(() =>
            {
                showReactionPicker = false;
                StateHasChanged();
            });
        }, null, 500, Timeout.Infinite);
    }

    /// <summary>
    /// Immediately hides the reaction picker.
    /// </summary>
    protected void HideReactionPicker()
    {
        showReactionPicker = false;
    }

    /// <summary>
    /// Handles when a reaction is picked from the reaction picker.
    /// </summary>
    /// <param name="reactionType">The selected reaction type.</param>
    protected async Task HandleReaction(LikeType reactionType)
    {
        showReactionPicker = false;
        await OnReactionChanged.InvokeAsync(reactionType);
    }

    /// <summary>
    /// Handles when the user removes their reaction.
    /// </summary>
    protected async Task HandleRemoveReaction()
    {
        showReactionPicker = false;
        if (OnRemoveReaction.HasDelegate)
        {
            await OnRemoveReaction.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles the default like button click.
    /// </summary>
    protected async Task OnLikeClick()
    {
        await OnReactionChanged.InvokeAsync(LikeType.Like);
    }

    #endregion

    #region Image Viewer Methods

    /// <summary>
    /// Opens the image viewer modal at the specified image index.
    /// </summary>
    /// <param name="imageIndex">The index of the image to display initially.</param>
    protected void OpenImageViewer(int imageIndex)
    {
        if (Images == null || !Images.Any()) return;

        // Request full-size images for the viewer by appending size=full query parameter
        imageUrls = Images.Select(img => $"{img.ImageUrl}?size=full").ToList();
        selectedImageIndex = imageIndex;
        showImageViewer = true;
        StateHasChanged();
    }

    /// <summary>
    /// Closes the image viewer modal.
    /// </summary>
    protected void CloseImageViewer()
    {
        showImageViewer = false;
        StateHasChanged();
    }

    #endregion

    #region Likes Modal Methods

    /// <summary>
    /// Closes the likes modal and clears the likes list.
    /// </summary>
    protected void CloseLikesModal()
    {
        showLikesModal = false;
        likes.Clear();
    }

    #endregion

    #region Menu Methods

    /// <summary>
    /// Toggles the post menu visibility.
    /// </summary>
    protected void ToggleMenu()
    {
        if (showMenu)
        {
            CloseMenu();
        }
        else
        {
            BuildMenuItems();
            showMenu = true;
            menuPositionStyle = "right: 0; top: 40px;";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Closes the post menu.
    /// </summary>
    protected void CloseMenu()
    {
        showMenu = false;
        StateHasChanged();
    }

    /// <summary>
    /// Builds the menu items. Override in derived classes to customize menu.
    /// </summary>
    protected abstract void BuildMenuItems();

    /// <summary>
    /// Shows the delete confirmation modal.
    /// </summary>
    protected void ShowDeleteConfirmation()
    {
        CloseMenu();
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    /// <summary>
    /// Cancels the delete confirmation and closes the modal.
    /// </summary>
    protected void CancelDeletePost()
    {
        showDeleteConfirmation = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles the edit post menu click.
    /// </summary>
    protected async Task HandleEditPostClick()
    {
        CloseMenu();

        if (OnEditPost.HasDelegate)
        {
            await OnEditPost.InvokeAsync(PostId);
        }
    }

    /// <summary>
    /// Handles the pin/unpin menu click.
    /// </summary>
    protected async Task HandlePinUnpinClick()
    {
        if (IsPinned)
        {
            if (OnUnpinPost.HasDelegate)
            {
                await OnUnpinPost.InvokeAsync(PostId);
            }
        }
        else
        {
            if (OnPinPost.HasDelegate)
            {
                await OnPinPost.InvokeAsync(PostId);
            }
        }
    }

    #endregion

    #region Comment Handler Methods

    /// <summary>
    /// Handles when the comment button is clicked.
    /// </summary>
    protected async Task OnCommentClick()
    {
        if (OnShowComments.HasDelegate)
        {
            await OnShowComments.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles when the share button is clicked.
    /// </summary>
    protected async Task OnShareClick()
    {
        await JS.InvokeVoidAsync("alert", "Share");
    }

    /// <summary>
    /// Handles when the comment count is clicked.
    /// </summary>
    protected async Task OnCommentCountClick()
    {
        if (CommentCount == 0) return;

        if (OnShowComments.HasDelegate)
        {
            await OnShowComments.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles when a comment is deleted, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="commentId">The ID of the comment that was deleted.</param>
    protected async Task OnCommentDeleted(int commentId)
    {
        if (OnDeleteComment.HasDelegate)
        {
            await OnDeleteComment.InvokeAsync(commentId);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Handles when a reply is submitted, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="args">Tuple containing the ParentCommentId and reply Content.</param>
    protected async Task OnReplySubmittedLocally((int ParentCommentId, string Content) args)
    {
        if (OnReplySubmitted.HasDelegate)
        {
            await OnReplySubmitted.InvokeAsync(args);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Handles when a comment is edited, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and CurrentContent.</param>
    protected async Task OnCommentEditedLocally((int CommentId, string CurrentContent) args)
    {
        if (OnEditComment.HasDelegate)
        {
            await OnEditComment.InvokeAsync(args);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Handles when a comment is successfully updated via inline editing, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and NewContent.</param>
    protected async Task OnCommentUpdatedLocally((int CommentId, string NewContent) args)
    {
        if (OnCommentUpdated.HasDelegate)
        {
            await OnCommentUpdated.InvokeAsync(args);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Handles when a comment reaction changes, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="args">Tuple containing the CommentId and ReactionType.</param>
    protected async Task OnCommentReactionChangedLocally((int CommentId, LikeType ReactionType) args)
    {
        if (OnCommentReactionChanged.HasDelegate)
        {
            await OnCommentReactionChanged.InvokeAsync(args);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Handles when a comment reaction is removed, forwarding to the parent and reloading internal comments if necessary.
    /// </summary>
    /// <param name="commentId">The ID of the comment whose reaction was removed.</param>
    protected async Task OnRemoveCommentReactionLocally(int commentId)
    {
        if (OnRemoveCommentReaction.HasDelegate)
        {
            await OnRemoveCommentReaction.InvokeAsync(commentId);

            if (LoadCommentsInternally)
            {
                await LoadCommentsAsync();
            }
        }
    }

    /// <summary>
    /// Loads comments asynchronously. Override in derived classes.
    /// </summary>
    protected abstract Task LoadCommentsAsync();

    #endregion

    #region Content Measurement Methods

    /// <summary>
    /// Handles when the content element is rendered for measurement.
    /// </summary>
    /// <param name="elementRef">The element reference.</param>
    protected Task HandleContentElementRendered(ElementReference elementRef)
    {
        contentElement = elementRef;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called from JavaScript to set whether content is overflowing (truncated).
    /// </summary>
    /// <param name="isOverflowing">Whether the content overflows its container.</param>
    [JSInvokable]
    public void SetContentOverflow(bool isOverflowing)
    {
        shouldShowExpandButton = isOverflowing;
        isTruncated = isOverflowing;
        StateHasChanged();
    }

    /// <summary>
    /// Shows the post detail view.
    /// </summary>
    protected async Task ShowPostDetail()
    {
        if (OnShowPostDetail.HasDelegate)
        {
            await OnShowPostDetail.InvokeAsync();
        }
    }

    #endregion

    #region IAsyncDisposable

    /// <summary>
    /// Disposes of managed resources asynchronously.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        hideTimer?.Dispose();

        if (module != null)
        {
            try
            {
                await module.InvokeVoidAsync("cleanupContentMeasurement");
                await module.DisposeAsync();
            }
            catch { }
        }
    }

    #endregion
}
