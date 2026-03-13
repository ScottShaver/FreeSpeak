# Developer Guide: Using PostPageBase Generic Base Component

## Overview

`PostPageBase<TPost, TComment>` is a generic Blazor base component that consolidates duplicate post interaction logic across multiple pages. It provides shared handler implementations for comments, replies, reactions, and pins that work for both regular posts (Post/Comment) and group posts (GroupPost/GroupPostComment).

## When to Use PostPageBase

Use `PostPageBase<TPost, TComment>` when creating a page that:
- Displays one or more posts in a list or single-post view
- Allows users to add comments/replies
- Supports post reactions (like, love, etc.)
- Handles comment reactions
- Supports post pinning/unpinning

## Generic Type Parameters

- **`TPost`**: The post entity type (e.g., `Post` or `GroupPost`)
- **`TComment`**: The comment entity type (e.g., `Comment` or `GroupPostComment`)

Both types must be reference types (`where TPost : class, where TComment : class`).

## Quick Start Example

### For Regular Posts (Post/Comment)

```csharp
@page "/my-feed"
@using FreeSpeakWeb.Components.Pages.Base
@inherits PostPageBase<Post, Comment>

@inject PostService PostService

<PageTitle>My Feed</PageTitle>

@foreach (var post in posts)
{
    <FeedArticle 
        PostId="@post.Id"
        OnCommentAdded="@HandleCommentAdded"
        OnReplySubmitted="@HandleReplySubmitted"
        OnReactionChanged="@((args) => HandlePostReactionChanged(args.PostId, args.ReactionType))"
        ... />
}

@code {
    private List<Post> posts = new();

    // Implement abstract properties
    protected override string? CurrentUserId => currentUserId;
    protected override string? CurrentUserImageUrl => currentUserImageUrl;
    protected override string? CurrentUserName => currentUserName;

    // Implement service bridge methods
    protected override async Task<(bool, string?)> AddCommentToPostAsync(int postId, string userId, string content)
    {
        var result = await PostService.AddCommentAsync(postId, userId, content);
        return (result.Success, result.ErrorMessage);
    }

    // ... implement remaining abstract methods
}
```

### For Group Posts (GroupPost/GroupPostComment)

```csharp
@page "/groups/{groupId:int}"
@using FreeSpeakWeb.Components.Pages.Base
@inherits PostPageBase<GroupPost, GroupPostComment>

@inject GroupPostService GroupPostService

@code {
    private List<GroupPost> groupPosts = new();

    // Same pattern, just use GroupPostService instead of PostService
    protected override async Task<(bool, string?)> AddCommentToPostAsync(int postId, string userId, string content)
    {
        var result = await GroupPostService.AddCommentAsync(postId, userId, content);
        return (result.Success, result.ErrorMessage);
    }
}
```

## Required Abstract Implementations

### 1. Properties (3 required)

```csharp
// User context - typically loaded in OnInitializedAsync
protected override string? CurrentUserId => currentUserId;
protected override string? CurrentUserImageUrl => currentUserImageUrl;
protected override string? CurrentUserName => currentUserName;
```

### 2. Service Bridge Methods (13 required)

All methods should delegate to the appropriate service (PostService or GroupPostService):

#### Comment Operations
```csharp
protected override async Task<(bool Success, string? ErrorMessage)> AddCommentToPostAsync(
    int postId, string userId, string content)
{
    var result = await PostService.AddCommentAsync(postId, userId, content);
    return (result.Success, result.ErrorMessage);
}

protected override async Task<(bool Success, string? ErrorMessage)> AddReplyToPostAsync(
    int postId, string userId, string content, int? imageId, int parentCommentId)
{
    var result = await PostService.AddCommentAsync(postId, userId, content, null, parentCommentId);
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

protected override async Task<int?> FindPostIdForCommentAsync(int commentId)
{
    var comment = await PostService.GetCommentByIdAsync(commentId);
    return comment?.PostId;
}
```

#### Post Reaction Operations
```csharp
protected override async Task<(bool Success, string? ErrorMessage)> AddOrUpdatePostReactionAsync(
    int postId, string userId, LikeType reactionType)
{
    var result = await PostService.AddOrUpdateReactionAsync(postId, userId, reactionType);
    return (result.Success, result.ErrorMessage);
}

protected override async Task<(bool Success, string? ErrorMessage)> RemovePostReactionAsync(
    int postId, string userId)
{
    var result = await PostService.RemoveLikeAsync(postId, userId);
    return (result.Success, result.ErrorMessage);
}

protected override async Task<Dictionary<LikeType, int>> GetPostReactionBreakdownAsync(int postId)
{
    return await PostService.GetReactionBreakdownAsync(postId);
}
```

#### Comment Reaction Operations
```csharp
protected override async Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(
    int commentId, string userId, LikeType reactionType)
{
    var result = await PostService.AddOrUpdateCommentReactionAsync(commentId, userId, reactionType);
    return (result.Success, result.ErrorMessage);
}

protected override async Task<(bool Success, string? ErrorMessage)> RemoveCommentReactionAsync(
    int commentId, string userId)
{
    var result = await PostService.RemoveCommentReactionAsync(commentId, userId);
    return (result.Success, result.ErrorMessage);
}
```

#### UI Update Methods
```csharp
protected override void IncrementPostCommentCount(int postId)
{
    var post = posts.FirstOrDefault(p => p.Id == postId);
    if (post != null) post.CommentCount++;
}

protected override void IncrementPostLikeCount(int postId)
{
    var post = posts.FirstOrDefault(p => p.Id == postId);
    if (post != null) post.LikeCount++;
}

protected override void DecrementPostLikeCount(int postId)
{
    var post = posts.FirstOrDefault(p => p.Id == postId);
    if (post != null) post.LikeCount--;
}
```

## Available Shared Handlers

The base component provides these handler methods that work automatically once you implement the abstract methods:

### Comment Handlers
- `HandleCommentAdded((int PostId, string Content) args)` - Adds comment and updates UI
- `HandleReplySubmitted((int ParentCommentId, string Content) args)` - Adds reply and updates UI

### Post Reaction Handlers
- `HandlePostReactionChanged(int postId, LikeType reactionType)` - Adds/updates reaction
- `HandleRemovePostReaction(int postId)` - Removes user's reaction

### Comment Reaction Handlers
- `HandleCommentReactionChanged((int CommentId, LikeType ReactionType) args)` - Adds/updates comment reaction
- `HandleRemoveCommentReaction(int commentId)` - Removes comment reaction

## Usage in Components

### Direct Handler Binding
```razor
<FeedArticle 
    OnCommentAdded="@HandleCommentAdded"
    OnReplySubmitted="@HandleReplySubmitted" />
```

### Lambda Wrapper (for parameter transformation)
```razor
<FeedArticle 
    OnReactionChanged="@((reactionType) => HandlePostReactionChanged(post.PostId, reactionType))" />
```

## Shared State Dictionaries

The base component provides these dictionaries for managing UI state:

```csharp
protected Dictionary<int, int> postRefreshTriggers = new();
protected Dictionary<int, Dictionary<LikeType, int>> postReactionData = new();
protected Dictionary<int, LikeType?> postUserReactions = new();
protected Dictionary<int, bool> pinnedPosts = new();
protected Dictionary<int, string> postAuthorNames = new();
```

Access these in your derived page as needed:
```csharp
var refreshTrigger = postRefreshTriggers.ContainsKey(postId) ? postRefreshTriggers[postId] : 0;
var userReaction = postUserReactions.ContainsKey(postId) ? postUserReactions[postId] : null;
```

## Helper Methods

### IncrementRefreshTrigger
```csharp
protected void IncrementRefreshTrigger(int postId)
```
Increments the refresh trigger for a post, forcing child components to re-render.

## Special Case: Single Post Pages

For pages that display a single post (like SinglePost.razor), you may want to reload the entire post after each interaction:

```csharp
protected override async Task<(bool, string?)> AddCommentToPostAsync(int postId, string userId, string content)
{
    var result = await PostService.AddCommentAsync(postId, userId, content);
    if (result.Success) await ReloadPost(); // Custom helper to refresh post
    return (result.Success, result.ErrorMessage);
}

private async Task ReloadPost()
{
    var updatedPost = await PostService.GetPublicPostByIdAsync(PostId, currentUserId);
    if (updatedPost.Success && updatedPost.Data != null)
    {
        post = updatedPost.Data;
        StateHasChanged();
    }
}
```

## Special Case: Multiple Post Lists

For pages that manage multiple lists (like Groups.razor with regular + pinned posts):

```csharp
protected override void IncrementPostCommentCount(int postId)
{
    // Update in main list
    var post = groupPosts.FirstOrDefault(p => p.Id == postId);
    if (post != null) post.CommentCount++;

    // Also update in pinned list
    var pinnedPost = pinnedGroupPostsList.FirstOrDefault(p => p.Id == postId);
    if (pinnedPost != null) pinnedPost.CommentCount++;
}
```

## Architecture Benefits

✅ **Single Source of Truth** - Fix bugs once, works everywhere  
✅ **Type Safety** - Generics ensure compile-time type checking  
✅ **Consistent Behavior** - All pages handle interactions identically  
✅ **Reduced Code** - ~150-250 lines eliminated per page  
✅ **Easier Testing** - Test base component once for all derived pages  
✅ **Flexible** - Override any behavior in derived pages as needed  

## Migration Checklist

When migrating an existing page to use PostPageBase:

- [ ] Add `@using FreeSpeakWeb.Components.Pages.Base`
- [ ] Add `@inherits PostPageBase<TPost, TComment>` with correct types
- [ ] Remove local dictionary definitions (use base class versions)
- [ ] Update dictionary references (`userReactions` → `postUserReactions`, etc.)
- [ ] Implement all 16 abstract methods (3 properties + 13 service bridges)
- [ ] Replace handler method implementations with delegates to base class
- [ ] Test all interactions (comments, replies, reactions, pins)
- [ ] Verify build succeeds
- [ ] Remove old handler method implementations

## When NOT to Use PostPageBase

Avoid using PostPageBase for:
- **Pages with mixed entity types** (e.g., Notifications.razor that shows both Post and GroupPost)
- **Pages without post interactions** (profile pages, settings, etc.)
- **Modals/dialogs** that handle their own state independently
- **Pages with radically different interaction patterns** that don't fit the base model

For mixed-entity pages, consider composition or helper services instead of inheritance.

## Troubleshooting

### Build Error: "does not implement inherited abstract member"
**Solution**: Ensure all 16 abstract methods are implemented (3 properties + 13 methods).

### Handler not firing
**Solution**: Check that component callback parameters match base handler signatures exactly.

### Dictionary key not found errors
**Solution**: Always check `ContainsKey()` before accessing dictionary values, or use the `TryGetValue()` pattern.

### Wrong service called
**Solution**: Verify you're using PostService for Post/Comment and GroupPostService for GroupPost/GroupPostComment.

## Examples in Codebase

Reference these files for complete working examples:
- `FreeSpeakWeb/Components/Pages/Home.razor` - Regular posts with post list
- `FreeSpeakWeb/Components/Pages/GroupView.razor` - Group posts with single group
- `FreeSpeakWeb/Components/Pages/Groups.razor` - Group posts with dual lists (regular + pinned)
- `FreeSpeakWeb/Components/Pages/SinglePost.razor` - Single post with reload pattern

## Further Reading

- [CODE_CONSOLIDATION_PLAN.md](CODE_CONSOLIDATION_PLAN.md) - Original refactoring plan
- [POSTPAGEBASE_MIGRATION_PROGRESS.md](POSTPAGEBASE_MIGRATION_PROGRESS.md) - Migration progress report
- ASP.NET Core Blazor component inheritance documentation
