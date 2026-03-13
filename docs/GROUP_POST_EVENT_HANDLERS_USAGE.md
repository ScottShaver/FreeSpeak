# GroupPostEventHandlers Usage Guide

## Overview

The `GroupPostEventHandlers` service consolidates common event handling logic for group posts across multiple page components (Groups.razor, GroupView.razor, Notifications.razor, etc.).

## Benefits

- ✅ **Eliminates code duplication** - Write event handling logic once
- ✅ **Consistency** - Same behavior across all pages
- ✅ **Easier maintenance** - Fix bugs in one place
- ✅ **Better testing** - Test handlers in isolation

## Setup

### 1. Inject the Service

```razor
@inject GroupPostEventHandlers EventHandlers
```

### 2. Define Your State Variables

```csharp
@code {
    private List<GroupPost> groupPosts = new();
    private List<GroupPost>? pinnedGroupPostsList = null; // Optional
    private Dictionary<int, List<CommentDisplayModel>> groupPostComments = new();
    private Dictionary<int, int> groupPostDirectCommentCounts = new();
    private Dictionary<int, LikeType?> groupPostUserReactions = new();
    private Dictionary<int, Dictionary<LikeType, int>> groupPostReactionData = new();
}
```

## Usage Examples

### Handling Comment Added

**Before (Duplicated in every page):**
```csharp
private async Task HandleGroupPostCommentAdded((int PostId, string Content) args)
{
    if (currentUserId == null) return;

    try
    {
        var result = await GroupPostService.AddCommentAsync(args.PostId, currentUserId, args.Content);

        if (result.Success)
        {
            var post = groupPosts.FirstOrDefault(p => p.Id == args.PostId);
            if (post != null)
            {
                post.CommentCount++;
            }

            var pinnedPost = pinnedGroupPostsList.FirstOrDefault(p => p.Id == args.PostId);
            if (pinnedPost != null)
            {
                pinnedPost.CommentCount++;
            }

            await LoadCommentsForGroupPost(args.PostId, 3);

            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding comment: {ex.Message}");
    }
}
```

**After (Using shared handler):**
```csharp
private async Task HandleGroupPostCommentAdded((int PostId, string Content) args)
{
    if (currentUserId == null) return;

    await EventHandlers.HandleCommentAddedAsync(
        args.PostId,
        groupPosts,
        pinnedGroupPostsList,  // Can be null if not using pinned posts
        groupPostComments,
        groupPostDirectCommentCounts,
        LoadCommentsForGroupPost
    );
    
    StateHasChanged();
}
```

### Handling Reply Submitted

**Before:**
```csharp
private async Task HandleGroupPostReplySubmitted((int ParentCommentId, string Content) args)
{
    if (currentUserId == null) return;

    try
    {
        int? postId = null;
        foreach (var kvp in groupPostComments)
        {
            if (FindCommentById(kvp.Value, args.ParentCommentId) != null)
            {
                postId = kvp.Key;
                break;
            }
        }

        if (postId == null) return;

        await LoadCommentsForGroupPost(postId.Value, 3);

        var post = groupPosts.FirstOrDefault(p => p.Id == postId.Value);
        if (post != null)
        {
            post.CommentCount++;
        }

        var pinnedPost = pinnedGroupPostsList.FirstOrDefault(p => p.Id == postId.Value);
        if (pinnedPost != null)
        {
            pinnedPost.CommentCount++;
        }

        StateHasChanged();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating UI after reply added: {ex.Message}");
    }
}
```

**After:**
```csharp
private async Task HandleGroupPostReplySubmitted((int ParentCommentId, string Content) args)
{
    if (currentUserId == null) return;

    await EventHandlers.HandleReplySubmittedAsync(
        args.ParentCommentId,
        groupPosts,
        pinnedGroupPostsList,
        groupPostComments,
        LoadCommentsForGroupPost
    );
    
    StateHasChanged();
}
```

### Handling Post Reactions

**Before:**
```csharp
private async Task HandleGroupPostReactionChanged(int postId, LikeType reactionType)
{
    if (currentUserId == null) return;

    try
    {
        var result = await GroupPostService.LikePostAsync(postId, currentUserId, reactionType);

        if (result.Success)
        {
            var previousReaction = groupPostUserReactions.ContainsKey(postId) ? groupPostUserReactions[postId] : null;
            groupPostUserReactions[postId] = reactionType;

            var reactions = await GroupPostService.GetReactionBreakdownAsync(postId);
            groupPostReactionData[postId] = reactions;

            var post = groupPosts.FirstOrDefault(p => p.Id == postId);
            if (post != null && previousReaction == null)
            {
                post.LikeCount++;
            }

            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling group post reaction: {ex.Message}");
    }
}
```

**After:**
```csharp
private async Task HandleGroupPostReactionChanged(int postId, LikeType reactionType)
{
    if (currentUserId == null) return;

    await EventHandlers.HandleReactionChangedAsync(
        postId,
        currentUserId,
        reactionType,
        groupPosts,
        groupPostUserReactions,
        groupPostReactionData
    );
    
    StateHasChanged();
}
```

### Handling Comment Reactions

**Before:**
```csharp
private async Task HandleGroupPostCommentReactionChanged((int CommentId, LikeType ReactionType) args)
{
    if (currentUserId == null) return;

    try
    {
        var result = await GroupPostService.LikeCommentAsync(args.CommentId, currentUserId, args.ReactionType);

        if (result.Success)
        {
            await UpdateGroupCommentReactionData(args.CommentId, args.ReactionType);
            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling group comment reaction: {ex.Message}");
    }
}
```

**After:**
```csharp
private async Task HandleGroupPostCommentReactionChanged((int CommentId, LikeType ReactionType) args)
{
    if (currentUserId == null) return;

    await EventHandlers.HandleCommentReactionChangedAsync(
        args.CommentId,
        currentUserId,
        args.ReactionType,
        groupPostComments
    );
    
    StateHasChanged();
}
```

## Complete Example

Here's a complete example showing how to refactor a page component:

```razor
@page "/groups"
@inject GroupPostEventHandlers EventHandlers
@inject GroupPostService GroupPostService

@code {
    private string? currentUserId;
    private List<GroupPost> groupPosts = new();
    private List<GroupPost> pinnedGroupPostsList = new();
    private Dictionary<int, List<CommentDisplayModel>> groupPostComments = new();
    private Dictionary<int, int> groupPostDirectCommentCounts = new();
    private Dictionary<int, LikeType?> groupPostUserReactions = new();
    private Dictionary<int, Dictionary<LikeType, int>> groupPostReactionData = new();

    // Comment handling
    private async Task HandleGroupPostCommentAdded((int PostId, string Content) args)
    {
        if (currentUserId == null) return;
        
        await EventHandlers.HandleCommentAddedAsync(
            args.PostId,
            groupPosts,
            pinnedGroupPostsList,
            groupPostComments,
            groupPostDirectCommentCounts,
            LoadCommentsForGroupPost
        );
        
        StateHasChanged();
    }

    // Reply handling
    private async Task HandleGroupPostReplySubmitted((int ParentCommentId, string Content) args)
    {
        if (currentUserId == null) return;
        
        await EventHandlers.HandleReplySubmittedAsync(
            args.ParentCommentId,
            groupPosts,
            pinnedGroupPostsList,
            groupPostComments,
            LoadCommentsForGroupPost
        );
        
        StateHasChanged();
    }

    // Reaction handling
    private async Task HandleGroupPostReactionChanged(int postId, LikeType reactionType)
    {
        if (currentUserId == null) return;
        
        await EventHandlers.HandleReactionChangedAsync(
            postId,
            currentUserId,
            reactionType,
            groupPosts,
            groupPostUserReactions,
            groupPostReactionData
        );
        
        StateHasChanged();
    }

    // Helper method (still needed)
    private async Task LoadCommentsForGroupPost(int postId, int count)
    {
        // Your existing implementation
    }
}
```

## Migration Checklist

When migrating a page to use `GroupPostEventHandlers`:

- [ ] Inject `GroupPostEventHandlers`
- [ ] Replace `HandleGroupPostCommentAdded` implementation
- [ ] Replace `HandleGroupPostReplySubmitted` implementation
- [ ] Replace `HandleGroupPostReactionChanged` implementation
- [ ] Replace `HandleRemoveGroupPostReaction` implementation
- [ ] Replace `HandleGroupPostCommentReactionChanged` implementation
- [ ] Replace `HandleRemoveGroupPostCommentReaction` implementation
- [ ] Remove duplicate helper methods (FindCommentById, etc.)
- [ ] Test all comment/reaction functionality
- [ ] Verify pinned posts work correctly (if applicable)

## Testing

The shared handlers can be unit tested independently:

```csharp
[Fact]
public async Task HandleCommentAddedAsync_UpdatesPostCount()
{
    // Arrange
    var handlers = new GroupPostEventHandlers(mockGroupPostService, mockLogger);
    var posts = new List<GroupPost> { new GroupPost { Id = 1, CommentCount = 0 } };
    
    // Act
    await handlers.HandleCommentAddedAsync(1, posts, null, comments, counts, LoadComments);
    
    // Assert
    Assert.Equal(1, posts[0].CommentCount);
}
```

## Future Improvements

Consider these enhancements:

1. **Return result objects** instead of void to indicate success/failure
2. **Add validation** before state updates
3. **Emit events** for UI notifications
4. **Support cancellation tokens** for long operations
5. **Add metrics/telemetry** for monitoring
