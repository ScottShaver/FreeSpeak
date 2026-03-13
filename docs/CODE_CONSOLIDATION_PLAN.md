# Code Consolidation Plan: Group Post Interaction Handlers

## Problem Statement

The same group post interaction logic (comments, replies, reactions, pins, etc.) is duplicated across multiple page components:
- `Groups.razor` (group feed aggregation page)
- `GroupView.razor` (individual group page)
- `Home.razor` (feed posts)
- `SinglePost.razor` (individual post view)
- `Notifications.razor` (notification interactions)

This leads to:
- ❌ **Bug Duplication**: Same bug must be fixed in 4+ places
- ❌ **Maintenance Nightmare**: Code changes require updates everywhere
- ❌ **Inconsistent Behavior**: Easy for implementations to drift apart
- ❌ **Testing Burden**: Same logic must be tested multiple times

## Immediate Fixes Applied

### GroupView.razor Bugs Fixed
1. **`HandleGroupPostCommentAdded`** - Now properly adds comments to database (was only updating UI)
2. **`HandleGroupPostReplySubmitted`** - Now always adds replies (removed incorrect conditional logic)

These match the pattern already used in `Groups.razor` and `Home.razor`.

## Recommended Solution: Create Reusable Handler Services

### Option 1: Service-Based Approach (Recommended)

Create a `GroupPostInteractionHandler` service that encapsulates all the interaction logic:

```csharp
public class GroupPostInteractionHandler
{
    private readonly GroupPostService _groupPostService;
    private readonly PinnedGroupPostService _pinnedGroupPostService;
    private readonly IJSRuntime _jsRuntime;

    public async Task<InteractionResult> HandleCommentAddedAsync(
        int postId, 
        string userId, 
        string content,
        Action<int> updateCommentCount,
        Action incrementRefreshTrigger)
    {
        var result = await _groupPostService.AddCommentAsync(postId, userId, content);

        if (result.Success)
        {
            updateCommentCount(postId);
            incrementRefreshTrigger();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert", result.ErrorMessage ?? "Failed to add comment");
        }

        return new InteractionResult { Success = result.Success };
    }

    public async Task<InteractionResult> HandleReplySubmittedAsync(
        int parentCommentId,
        string userId,
        string content,
        Action<int> updateCommentCount,
        Action incrementRefreshTrigger)
    {
        var parentComment = await _groupPostService.GetCommentByIdAsync(parentCommentId);
        if (parentComment == null) return InteractionResult.NotFound("Parent comment not found");

        var result = await _groupPostService.AddCommentAsync(
            parentComment.PostId, 
            userId, 
            content, 
            null, 
            parentCommentId);

        if (result.Success)
        {
            updateCommentCount(parentComment.PostId);
            incrementRefreshTrigger();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert", result.ErrorMessage ?? "Failed to add reply");
        }

        return new InteractionResult { Success = result.Success };
    }

    // Similar methods for:
    // - HandleReactionChangedAsync
    // - HandleRemoveReactionAsync
    // - HandlePinPostAsync
    // - HandleUnpinPostAsync
    // - HandleCommentReactionChangedAsync
    // - HandleRemoveCommentReactionAsync
}
```

### Option 2: Base Component Approach (RECOMMENDED - ENHANCED)

Create a **generic `PostPageBase`** component that handles BOTH regular posts and group posts:

```csharp
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

    // Abstract properties - implemented by derived pages
    protected abstract string? CurrentUserId { get; }
    protected abstract string? CurrentUserImageUrl { get; }
    protected abstract string? CurrentUserName { get; }

    // Abstract methods for service access - allows working with Post OR GroupPost
    protected abstract Task<(bool Success, string? ErrorMessage)> AddCommentToPostAsync(int postId, string userId, string content);
    protected abstract Task<(bool Success, string? ErrorMessage)> AddReplyToPostAsync(int postId, string userId, string content, int? imageId, int parentCommentId);
    protected abstract Task<TComment?> GetCommentByIdAsync(int commentId);
    protected abstract int GetPostIdFromComment(TComment comment);
    protected abstract void IncrementPostCommentCount(int postId);

    // Shared handler implementations
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

    // Shared helper methods
    protected void IncrementRefreshTrigger(int postId)
    {
        if (!postRefreshTriggers.ContainsKey(postId))
        {
            postRefreshTriggers[postId] = 0;
        }
        postRefreshTriggers[postId]++;
    }

    // Additional shared handlers for reactions, pins, etc.
    // All implemented once, work for both Post and GroupPost!
}
```

**Example Usage - GroupView.razor:**
```csharp
@inherits PostPageBase<GroupPost, GroupPostComment>

@code {
    [Inject] protected GroupPostService GroupPostService { get; set; } = default!;
    [Inject] protected PinnedGroupPostService PinnedGroupPostService { get; set; } = default!;

    private List<GroupPost> groupPosts = new();
    private string? currentUserId;
    private string? currentUserImageUrl;
    private string? currentUserName;

    // Implement abstract properties
    protected override string? CurrentUserId => currentUserId;
    protected override string? CurrentUserImageUrl => currentUserImageUrl;
    protected override string? CurrentUserName => currentUserName;

    // Implement abstract methods - bridge to GroupPostService
    protected override async Task<(bool, string?)> AddCommentToPostAsync(int postId, string userId, string content)
    {
        var result = await GroupPostService.AddCommentAsync(postId, userId, content);
        return (result.Success, result.ErrorMessage);
    }

    protected override async Task<(bool, string?)> AddReplyToPostAsync(int postId, string userId, string content, int? imageId, int parentCommentId)
    {
        var result = await GroupPostService.AddCommentAsync(postId, userId, content, imageId, parentCommentId);
        return (result.Success, result.ErrorMessage);
    }

    protected override async Task<GroupPostComment?> GetCommentByIdAsync(int commentId)
    {
        return await GroupPostService.GetCommentByIdAsync(commentId);
    }

    protected override int GetPostIdFromComment(GroupPostComment comment)
    {
        return comment.PostId;
    }

    protected override void IncrementPostCommentCount(int postId)
    {
        var post = groupPosts.FirstOrDefault(p => p.Id == postId);
        if (post != null) post.CommentCount++;
    }

    // Page-specific methods only (loading data, navigation, etc.)
    private async Task LoadGroupPosts() { /* ... */ }
}
```

**Example Usage - Home.razor:**
```csharp
@inherits PostPageBase<Post, Comment>

@code {
    [Inject] protected PostService PostService { get; set; } = default!;

    private List<Post> posts = new();

    // Implement abstract methods - bridge to PostService
    protected override async Task<(bool, string?)> AddCommentToPostAsync(int postId, string userId, string content)
    {
        var result = await PostService.AddCommentAsync(postId, userId, content);
        return (result.Success, result.ErrorMessage);
    }

    protected override async Task<Comment?> GetCommentByIdAsync(int commentId)
    {
        return await PostService.GetCommentByIdAsync(commentId);
    }

    protected override int GetPostIdFromComment(Comment comment)
    {
        return comment.PostId;
    }

    // ... rest is same pattern as GroupView
}
```

### Why This Approach is Better

1. **Maximum Code Reuse**: Single implementation works for Post AND GroupPost
2. **Type Safety**: Generics ensure compile-time type checking
3. **Flexibility**: Each page can customize behavior via abstract methods
4. **No Service Duplication**: Don't need separate GroupPostInteractionHandler and FeedPostInteractionHandler
5. **Easier Testing**: Test the base class once with both entity types

### Option 3: Hybrid Approach (Alternative)

If we need even more flexibility, we could combine the base component with helper services:

1. **`PostPageBase<TPost, TComment>`** - Handles UI state management and common patterns
2. **`PostInteractionService<TPost, TComment>`** - Handles complex business logic
3. **Page Components** - Minimal glue code between base and services

However, **Option 2 (Enhanced Base Component) is recommended** because:
- ✅ Simpler architecture (fewer moving parts)
- ✅ Better for Blazor component model
- ✅ Easier to debug (all logic in one inheritance chain)
- ✅ No extra service registrations needed
- ✅ Works perfectly for both Post and GroupPost entities

## Implementation Steps (Enhanced Approach)

### Phase 1: Create the Base Component
1. Create `PostPageBase<TPost, TComment>` in `FreeSpeakWeb/Components/Pages/Base/`
2. Implement all shared handler methods:
   - `HandleCommentAdded`
   - `HandleReplySubmitted`
   - `HandleReactionChanged`
   - `HandleRemoveReaction`
   - `HandlePinPost`
   - `HandleUnpinPost`
   - `HandleCommentReactionChanged`
   - `HandleRemoveCommentReaction`
3. Add XML documentation for abstract methods
4. Create unit tests for the base component

### Phase 2: Migrate Group Post Pages
1. Update `GroupView.razor` to inherit from `PostPageBase<GroupPost, GroupPostComment>`
   - Implement abstract methods as bridges to GroupPostService
   - Remove all duplicated handler code
   - Test thoroughly
2. Update `Groups.razor` (same pattern)
3. Update `Notifications.razor` (for group post interactions)

### Phase 3: Migrate Feed Post Pages
1. Update `Home.razor` to inherit from `PostPageBase<Post, Comment>`
   - Implement abstract methods as bridges to PostService
   - Remove all duplicated handler code
   - Test thoroughly
2. Update `SinglePost.razor` (same pattern)

### Phase 4: Cleanup and Enhancement
1. Remove all duplicated handler code from original pages
2. Add comprehensive integration tests
3. Create migration guide document for future pages
4. Update developer documentation

### Phase 5: Consider Additional Abstractions
After consolidation, evaluate if similar patterns can be applied to:
- Image upload handlers
- Notification handlers
- User profile interaction handlers

## Files to Create/Modify

### New Files
- `FreeSpeakWeb/Components/Pages/Base/PostPageBase.cs` ⭐ (the key file)
- `FreeSpeakWeb.Tests/Components/Pages/Base/PostPageBaseTests.cs`
- `docs/DEVELOPER_GUIDE_BASE_COMPONENTS.md` (usage guide)

### Files to Modify (reduce code by ~60%)
- `FreeSpeakWeb/Components/Pages/GroupView.razor` - Remove ~200 lines
- `FreeSpeakWeb/Components/Pages/Groups.razor` - Remove ~250 lines
- `FreeSpeakWeb/Components/Pages/Home.razor` - Remove ~200 lines
- `FreeSpeakWeb/Components/Pages/SinglePost.razor` - Remove ~150 lines
- `FreeSpeakWeb/Components/Pages/Notifications.razor` - Remove ~100 lines

**Total Code Reduction**: ~900 lines → ~300 lines + 1 base component (150 lines)
**Net Savings**: ~750 lines of duplicated code eliminated!

## Benefits After Consolidation

✅ **Single Source of Truth**: Fix bugs in ONE place
✅ **Easier Maintenance**: Changes propagate automatically
✅ **Consistent Behavior**: All pages work identically
✅ **Better Testing**: Test once, works everywhere
✅ **Faster Development**: New features added once

## Similar Patterns to Consider

After fixing group posts, apply the same pattern to:
- Feed post interactions (PostService handlers)
- User interactions (friendship requests, profile updates)
- Notification interactions

## Files Affected

### Current Duplication
- `FreeSpeakWeb/Components/Pages/Groups.razor`
- `FreeSpeakWeb/Components/Pages/GroupView.razor`
- `FreeSpeakWeb/Components/Pages/Home.razor`
- `FreeSpeakWeb/Components/Pages/SinglePost.razor`
- `FreeSpeakWeb/Components/Pages/Notifications.razor`

### New Files to Create
- `FreeSpeakWeb/Services/GroupPostInteractionHandler.cs`
- `FreeSpeakWeb/Services/FeedPostInteractionHandler.cs` (for Home/SinglePost)
- `FreeSpeakWeb.Tests/Services/GroupPostInteractionHandlerTests.cs`

### Files to Update
- `FreeSpeakWeb/Program.cs` (register new services)

## Estimated Effort

- **Phase 1**: 2-3 hours (create service + tests)
- **Phase 2**: 1 hour (migrate first page)
- **Phase 3**: 3-4 hours (migrate remaining pages)
- **Phase 4**: 1 hour (cleanup + documentation)

**Total**: ~7-9 hours for complete consolidation

## Risk Mitigation

- ✅ Existing pages keep working during migration (incremental approach)
- ✅ Comprehensive tests ensure behavior stays consistent
- ✅ Can roll back per-page if issues arise
- ✅ No database schema changes required

---

**Recommendation**: Schedule Phase 1 & 2 for next sprint. This is a significant technical debt that will prevent future bugs and speed up development.
