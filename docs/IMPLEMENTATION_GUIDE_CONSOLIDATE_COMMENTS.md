# Implementation Guide: Consolidate Group Post Comments into GroupPostArticle

## Executive Summary

**Problem**: Comments display inconsistently:
- GroupView: No comments at all
- Groups (Feed): Only top-level, no child comments  
- Modal: Everything works

**Root Cause**: GroupPostArticle is passive - receives Comments from parent. Each parent loads differently.

**Solution**: Make GroupPostArticle self-contained with internal comment loading.

## Implementation Strategy

### Phase 1: Add Self-Loading to GroupPostArticle (Backward Compatible)

Add new parameters:
```csharp
[Parameter] public bool LoadCommentsInternally { get; set; } = false;
[Parameter] public int CommentsToShow { get; set; } = 3;
```

Add internal state:
```csharp
private List<CommentDisplayModel> internalComments = new();
private int internalDirectCommentCount = 0;
private bool isLoadingComments = false;
```

Add lifecycle hook:
```csharp
protected override async Task OnParametersSetAsync()
{
    if (LoadCommentsInternally && !isLoadingComments)
    {
        await LoadCommentsAsync();
    }
}
```

Add loading method:
```csharp
private async Task LoadCommentsAsync()
{
    if (isLoadingComments) return;
    isLoadingComments = true;
    
    try
    {
        var allComments = await GroupPostService.GetCommentsAsync(PostId);
        
        // Select N newest, display oldest-first (per spec)
        var topComments = allComments
            .OrderByDescending(c => c.CreatedAt)
            .Take(CommentsToShow)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        
        // Build comment display models with full nesting
        internalComments = await BuildCommentModelsAsync(topComments);
        internalDirectCommentCount = allComments.Count(c => c.ParentCommentId == null);
    }
    finally
    {
        isLoadingComments = false;
    }
}

private async Task<List<CommentDisplayModel>> BuildCommentModelsAsync(List<GroupPostComment> comments)
{
    var models = new List<CommentDisplayModel>();
    foreach (var c in comments)
    {
        models.Add(await BuildCommentModelAsync(c));
    }
    return models;
}

private async Task<CommentDisplayModel> BuildCommentModelAsync(GroupPostComment comment)
{
    // Build nested replies recursively
    var replyModels = new List<CommentDisplayModel>();
    if (comment.Replies != null && comment.Replies.Any())
    {
        foreach (var reply in comment.Replies)
        {
            replyModels.Add(await BuildCommentModelAsync(reply));
        }
    }
    
    // Get user display name
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
    
    // Get user's reaction
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
        Replies = replyModels,
        LikeCount = 0, // TODO: Get actual like count
        UserReaction = userReaction,
        ReactionBreakdown = new Dictionary<LikeType, int>()
    };
}
```

Update rendering to use internal or external comments:
```razor
@{
    var commentsToDisplay = LoadCommentsInternally ? internalComments : (Comments ?? new List<CommentDisplayModel>());
    var directCount = LoadCommentsInternally ? internalDirectCommentCount : DirectCommentCount;
}

<!-- More Comments Indicator -->
@if (!IsModalView && directCount > 0 && commentsToDisplay != null && directCount > commentsToDisplay.Count)
{
    <div class="more-comments-indicator">
        <button type="button" class="more-comments-button" @onclick="OnCommentClick">
            View more comments
        </button>
    </div>
}

<!-- 5. Display Existing Comments -->
@if (commentsToDisplay != null && commentsToDisplay.Any())
{
    <div class="article-comments">
        @foreach (var comment in commentsToDisplay)
        {
            <MultiLineCommentDisplay 
                CommentId="@comment.CommentId"
                ... />
        }
    </div>
}
```

### Phase 2: Update Groups.razor to Use Self-Loading

**Before:**
```razor
@foreach (var post in groupPosts)
{
    var comments = groupPostComments.ContainsKey(post.Id) ? groupPostComments[post.Id] : new List<CommentDisplayModel>();
    var directCommentCount = groupPostDirectCommentCounts.ContainsKey(post.Id) ? groupPostDirectCommentCounts[post.Id] : 0;
    
    <GroupPostArticle 
        PostId="@post.Id"
        Comments="@comments"
        DirectCommentCount="@directCommentCount"
        ... />
}
```

**After:**
```razor
@foreach (var post in groupPosts)
{
    <GroupPostArticle 
        PostId="@post.Id"
        LoadCommentsInternally="true"
        CommentsToShow="3"
        ... />
}
```

**Remove from Groups.razor:**
- `LoadCommentsForGroupPost` method
- `BuildGroupCommentDisplayModel` method
- `groupPostComments` dictionary
- `groupPostDirectCommentCounts` dictionary
- Calls to `LoadCommentsForGroupPost` in `LoadGroupPostDataAsync`

### Phase 3: Update GroupView.razor

Same changes as Groups.razor.

### Phase 4: Handle Comment Addition

When a comment is added, GroupPostArticle needs to reload:

```csharp
private async Task OnCommentSubmitted(string commentText)
{
    if (OnCommentAdded.HasDelegate && !string.IsNullOrWhiteSpace(commentText))
    {
        await OnCommentAdded.InvokeAsync((PostId, commentText));
        
        // If loading internally, reload comments
        if (LoadCommentsInternally)
        {
            await LoadCommentsAsync();
        }
    }
}
```

Update parent's HandleGroupPostCommentAdded:
```csharp
private async Task HandleGroupPostCommentAdded((int PostId, string Content) args)
{
    if (currentUserId == null) return;

    try
    {
        // Add comment to database
        var result = await GroupPostService.AddCommentAsync(args.PostId, currentUserId, args.Content);

        if (result.Success)
        {
            // Update post comment count
            var post = groupPosts.FirstOrDefault(p => p.Id == args.PostId);
            if (post != null)
            {
                post.CommentCount++;
            }

            // GroupPostArticle will reload its own comments
            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding comment: {ex.Message}");
    }
}
```

### Phase 5: Add Required Services

GroupPostArticle needs access to:
```csharp
@inject GroupPostService GroupPostService
@inject UserPreferenceService UserPreferenceService
```

## Complete File Changes

### 1. GroupPostArticle.razor

**Add at top:**
```razor
@inject GroupPostService GroupPostService
@inject UserPreferenceService UserPreferenceService
```

**Add parameters:**
```csharp
[Parameter] public bool LoadCommentsInternally { get; set; } = false;
[Parameter] public int CommentsToShow { get; set; } = 3;
```

**Add state:**
```csharp
private List<CommentDisplayModel> internalComments = new();
private int internalDirectCommentCount = 0;
private bool isLoadingComments = false;
```

**Add methods:** (as shown above)
- LoadCommentsAsync
- BuildCommentModelsAsync
- BuildCommentModelAsync

**Update OnParametersSet:**
```csharp
protected override async Task OnParametersSetAsync()
{
    if (!hasLocalPinnedChange)
    {
        currentIsPinned = IsPinned;
    }
    else if (currentIsPinned == IsPinned)
    {
        hasLocalPinnedChange = false;
    }
    
    // Load comments if internal loading enabled
    if (LoadCommentsInternally && !isLoadingComments)
    {
        await LoadCommentsAsync();
    }
}
```

**Update rendering:** (as shown above)

### 2. Groups.razor

**Remove:**
- `private Dictionary<int, List<CommentDisplayModel>> groupPostComments = new();`
- `private Dictionary<int, int> groupPostDirectCommentCounts = new();`
- `LoadCommentsForGroupPost` method
- `BuildGroupCommentDisplayModel` method
- Call to `await LoadCommentsForGroupPost(post.Id, 3);` in `LoadGroupPostDataAsync`

**Update GroupPostArticle usage:**
```razor
<GroupPostArticle 
    PostId="@post.Id"
    LoadCommentsInternally="true"
    CommentsToShow="3"
    ... />
```

**Simplify HandleGroupPostCommentAdded:**
```csharp
private async Task HandleGroupPostCommentAdded((int PostId, string Content) args)
{
    if (currentUserId == null) return;

    var result = await GroupPostService.AddCommentAsync(args.PostId, currentUserId, args.Content);

    if (result.Success)
    {
        var post = groupPosts.FirstOrDefault(p => p.Id == args.PostId);
        if (post != null)
        {
            post.CommentCount++;
        }
        
        StateHasChanged();
    }
}
```

### 3. GroupView.razor

Same changes as Groups.razor.

## Testing Checklist

After implementation:

### GroupView (Individual Group Page)
- [ ] Comments display
- [ ] Top-level comments show
- [ ] Child comments (replies) show
- [ ] 3rd level comments show
- [ ] 4th level comments show
- [ ] Ordering is oldest-to-newest
- [ ] Add comment works
- [ ] Add reply works
- [ ] Comment reactions work

### Groups (My Group Feed)
- [ ] Comments display
- [ ] Top-level comments show
- [ ] Child comments (replies) show
- [ ] Shows 3 most recent top-level comments
- [ ] Ordering is oldest-to-newest
- [ ] "View more comments" button shows when needed
- [ ] Add comment works
- [ ] Add reply works
- [ ] Comment reactions work

### GroupPostDetailModal
- [ ] Comments display
- [ ] All comments show (not just 3)
- [ ] Nested comments show
- [ ] Pagination works
- [ ] Add comment works
- [ ] Add reply works
- [ ] Consistent with feed

## Migration Steps

1. ✅ Create backup branch
2. ✅ Implement Phase 1 (add self-loading to GroupPostArticle)
3. ✅ Test GroupPostArticle in isolation
4. ✅ Implement Phase 2 (update Groups.razor)
5. ✅ Test My Group Feed thoroughly
6. ✅ Implement Phase 3 (update GroupView.razor)
7. ✅ Test individual group pages
8. ✅ Run full test suite
9. ✅ Update documentation
10. ✅ Merge to main branch

## Rollback Plan

If issues occur:
1. Keep `LoadCommentsInternally` parameter
2. Set to `false` to revert to old behavior
3. Keep old comment loading code temporarily
4. Debug and fix issues
5. Re-enable internal loading

## Performance Considerations

### Before (Current)
- Parent loads comments once
- Passes to GroupPostArticle
- GroupPostArticle re-renders when Comments change

### After (Proposed)
- GroupPostArticle loads comments in OnParametersSetAsync
- May trigger extra loads if parameters change frequently
- **Mitigation**: Track PostId changes, only reload if PostId changed

```csharp
private int? lastLoadedPostId = null;

protected override async Task OnParametersSetAsync()
{
    if (LoadCommentsInternally && (!isLoadingComments && lastLoadedPostId != PostId))
    {
        lastLoadedPostId = PostId;
        await LoadCommentsAsync();
    }
}
```

## Next Steps

1. Review this implementation guide
2. Implement Phase 1 in GroupPostArticle
3. Test with a simple test page
4. Once Phase 1 works, proceed to Phase 2
5. Migrate one parent at a time
6. Test thoroughly between each phase

This is a significant architectural improvement that will make your codebase much more maintainable!
