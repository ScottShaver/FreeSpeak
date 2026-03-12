# Fix: Modal Reply Not Updating Feed

## Problem
When a reply comment was made in the GroupPostDetailModal and the modal was closed, the corresponding GroupPostArticle in the feed list did not update to show the new reply comment.

## Root Cause
`GroupPostArticle` with `LoadCommentsInternally="true"` only reloads comments in `OnParametersSetAsync` when the `PostId` changes. When a reply is added to an existing post:
- The PostId remains the same
- No parameters change
- `OnParametersSetAsync` doesn't trigger a reload
- The feed continues showing the old comment state

## Solution
Implemented a **RefreshTrigger** parameter pattern:

### 1. Added RefreshTrigger Parameter to GroupPostArticle
```csharp
[Parameter]
public int RefreshTrigger { get; set; } = 0;

private int lastRefreshTrigger = 0;
```

### 2. Updated OnParametersSetAsync to Watch RefreshTrigger
```csharp
// Reload if PostId changes OR RefreshTrigger changes
if (LoadCommentsInternally && !isLoadingComments && 
    (lastLoadedPostId != PostId || lastRefreshTrigger != RefreshTrigger))
{
    lastLoadedPostId = PostId;
    lastRefreshTrigger = RefreshTrigger;
    await LoadCommentsAsync();
}
```

### 3. Added RefreshTrigger Dictionary in Groups.razor
```csharp
private Dictionary<int, int> groupPostRefreshTriggers = new();
```

### 4. Pass RefreshTrigger to GroupPostArticle
```razor
var refreshTrigger = groupPostRefreshTriggers.ContainsKey(post.Id) 
    ? groupPostRefreshTriggers[post.Id] : 0;

<GroupPostArticle 
    PostId="@post.Id"
    RefreshTrigger="@refreshTrigger"
    LoadCommentsInternally="true"
    ... />
```

### 5. Increment RefreshTrigger When Comments/Replies Added
```csharp
// In HandleGroupPostCommentAdded and HandleGroupPostReplySubmitted
if (!groupPostRefreshTriggers.ContainsKey(postId))
{
    groupPostRefreshTriggers[postId] = 0;
}
groupPostRefreshTriggers[postId]++;
StateHasChanged();
```

## How It Works

1. **Initial State**: All posts have `RefreshTrigger = 0`
2. **Reply Added in Modal**: 
   - Modal adds reply to database
   - Modal triggers `HandleGroupPostReplySubmitted` in parent (Groups.razor)
   - Handler increments `groupPostRefreshTriggers[postId]` (0 → 1)
   - Handler calls `StateHasChanged()`
3. **Re-Render**:
   - Groups.razor re-renders
   - GroupPostArticle receives `RefreshTrigger="1"` (was 0)
   - `OnParametersSetAsync` detects `lastRefreshTrigger (0) != RefreshTrigger (1)`
   - Calls `LoadCommentsAsync()`
   - Comments reload with new reply included
4. **Subsequent Replies**:
   - RefreshTrigger increments again (1 → 2, 2 → 3, etc.)
   - Each increment triggers a reload

## Benefits

✅ **Decoupled**: GroupPostArticle doesn't need to know about modal state  
✅ **Reusable**: Same pattern works for top-level comments and replies  
✅ **Clean**: No need for public refresh methods or complex event handling  
✅ **Reliable**: Blazor's parameter change detection ensures reload happens  

## Files Changed

- `FreeSpeakWeb/Components/SocialFeed/GroupPostArticle.razor` - Added RefreshTrigger parameter and logic
- `FreeSpeakWeb/Components/Pages/Groups.razor` - Added refresh trigger tracking and increment logic
- `FreeSpeakWeb/Components/SocialFeed/MultiLineCommentDisplay.razor` - Removed debug logging

## Testing

✅ Add reply in modal → Close modal → Feed updates  
✅ Add top-level comment → Feed updates  
✅ Multiple replies → Each triggers update  
✅ Pinned posts tab → Also updates correctly  

## Related
- See `IMPLEMENTATION_GUIDE_CONSOLIDATE_COMMENTS.md` for the overall consolidation strategy
- See `DEBUG_CHILD_COMMENTS_NOT_SHOWING.md` for the investigation that confirmed child comments display correctly
