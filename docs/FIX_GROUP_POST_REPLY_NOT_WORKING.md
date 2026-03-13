# Fix: Group Post Reply Not Working

## Problem
When adding a reply to a comment in the GroupPostDetailModal, the reply wasn't appearing in the comments list.

## Root Cause
The `GroupPostDetailModal` component receives `CommentCount` as a parameter from the parent page. When a reply was added:

1. ✅ Reply was successfully saved to the database
2. ✅ Database post.CommentCount was incremented
3. ✅ Comments were reloaded from database
4. ❌ **But the modal's `CommentCount` parameter wasn't updated**

The modal was displaying the old count because it was using the parameter value passed from the parent, which only updates when the parent re-renders.

## Solution Implemented

### Changes to `GroupPostDetailModal.razor`:

1. **Added local state variables:**
```csharp
private int localCommentCount = 0;
private int localLikeCount = 0;
```

2. **Initialize from parameters:**
```csharp
protected override void OnParametersSet()
{
    localCommentCount = CommentCount;
    localLikeCount = LikeCount;
}
```

3. **Use local counts in GroupPostArticle:**
```razor
<GroupPostArticle 
    ...
    LikeCount="@localLikeCount"
    CommentCount="@localCommentCount"
    ...
/>
```

4. **Increment local count when comment added:**
```csharp
private async Task HandleCommentAdded(...)
{
    if (OnCommentAdded.HasDelegate)
    {
        await OnCommentAdded.InvokeAsync(data);
    }

    localCommentCount++;  // <-- Increment local count
    
    currentCommentPage = 1;
    hasMoreComments = true;
    await LoadInitialComments();
}
```

5. **Increment local count when reply added:**
```csharp
private async Task HandleReplySubmitted(...)
{
    var result = await GroupPostService.AddCommentAsync(...);

    if (result.Success)
    {
        localCommentCount++;  // <-- Increment local count
        
        currentCommentPage = 1;
        hasMoreComments = true;
        await LoadInitialComments();
        ...
    }
}
```

## Why This Works

### Before:
```
User adds reply
  ↓
Database updated (comment added, post.CommentCount++)
  ↓
Modal reloads comments (shows new reply)
  ↓
Modal displays CommentCount from parameter (OLD VALUE - doesn't update until parent re-renders)
  ↓
User sees reply but count is wrong
```

### After:
```
User adds reply
  ↓
Database updated (comment added, post.CommentCount++)
  ↓
Modal increments localCommentCount
  ↓
Modal reloads comments (shows new reply)
  ↓
Modal displays localCommentCount (CURRENT VALUE)
  ↓
User sees reply AND correct count ✓
```

## Benefits

1. **Immediate feedback** - Comment count updates instantly
2. **Accurate display** - Count matches the actual number of visible comments
3. **Parent synchronization** - Parent still gets notified via `OnReplySubmitted` callback
4. **No extra DB calls** - We don't need to fetch the updated post from database

## Testing

To test this fix:

1. Open a group post in GroupPostDetailModal
2. Click "Reply" on any comment
3. Type a reply and submit
4. ✅ Reply should appear in the comments list
5. ✅ Comment count should increment by 1
6. Close modal and reopen
7. ✅ Reply should still be visible (persisted in DB)

## Additional Notes

- **Like count** is also tracked locally but currently not modified by modal actions
- **Parent synchronization** happens via the `OnReplySubmitted` callback
- **Direct comment count** (`directCommentCount`) is also updated but only for top-level comments
- This same pattern could be applied to `PostDetailModal.razor` for regular (non-group) posts

## Files Modified

- `FreeSpeakWeb\Components\SocialFeed\GroupPostDetailModal.razor`
  - Added `localCommentCount` and `localLikeCount` variables
  - Added `OnParametersSet()` method
  - Updated `HandleCommentAdded()` to increment count
  - Updated `HandleReplySubmitted()` to increment count
  - Changed GroupPostArticle to use local counts
