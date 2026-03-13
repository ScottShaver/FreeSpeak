# Fix: 4th Level Nested Comments Not Displaying in Feed

## Problem
In the GroupPostDetailModal, all 4 levels of nested comments displayed correctly. However, in the feed lists (My Group Feed and Group page), only 3 levels were displaying - the 4th level was missing.

## Root Cause
The issue was in `GroupPostService.GetCommentsAsync()` method. While it was loading 4 levels of comments using EF Core's `.Include().ThenInclude()` pattern, it was **not loading the Replies navigation property for the 4th level**.

### What Was Happening

**EF Core Include Pattern:**
```csharp
.Include(c => c.Replies)                           // Level 2
    .ThenInclude(r => r.Replies)                   // Level 3
        .ThenInclude(rr => rr.Replies)             // Level 4
            .ThenInclude(rrr => rrr.Author)        // Level 4 Authors ONLY
```

This loaded:
- ✅ Level 1: Top-level comments (c)
- ✅ Level 2: c.Replies
- ✅ Level 3: c.Replies.Replies  
- ✅ Level 4: c.Replies.Replies.Replies (entities)
- ❌ Level 4: **Replies collection was NULL/empty** (not loaded)

### Why the Modal Worked

The modal uses a different approach:
```csharp
private async Task<CommentDisplayModel> BuildCommentDisplayModel(GroupPostComment comment)
{
    // Recursively loads each level separately
    var replies = await GroupPostService.GetRepliesAsync(comment.Id);
    
    foreach (var reply in replies)
    {
        var replyModel = await BuildCommentDisplayModel(reply); // Recursive
        replyModels.Add(replyModel);
    }
    ...
}
```

This makes **separate database calls** for each level, so it always loads the full hierarchy.

### Why the Feed Didn't Work

`GroupPostArticle.BuildCommentModelAsync()` relies on the `comment.Replies` navigation property being populated:

```csharp
if (comment.Replies != null && comment.Replies.Any())
{
    foreach (var reply in comment.Replies)
    {
        replyModels.Add(await BuildCommentModelAsync(reply));
    }
}
```

If `comment.Replies` isn't loaded for level 4 comments, it would be `null` or empty, and no nested replies would be rendered.

## Solution

Added one more `.ThenInclude()` chain to load the Replies collection for level 4 comments:

```csharp
.Include(c => c.Replies)
    .ThenInclude(r => r.Replies)
        .ThenInclude(rr => rr.Replies)
            .ThenInclude(rrr => rrr.Replies)           // Load Level 4's Replies
                .ThenInclude(rrrr => rrrr.Author)      // And their authors
```

Now the structure is:
- Level 1: c (top-level)
- Level 2: c.Replies (r)
- Level 3: c.Replies.Replies (rr)
- Level 4: c.Replies.Replies.Replies (rrr)
- **Level 4's Replies**: c.Replies.Replies.Replies.Replies (rrrr) ✨

Even though Level 5 won't be displayed (MaxFeedPostCommentDepth = 4), we need to load the Replies collection so the recursive rendering knows they exist.

## Files Modified

- `FreeSpeakWeb/Services/GroupPostService.cs` - Added 4th level Replies navigation property loading

## Testing

### Before Fix
- ❌ Feed: 1st, 2nd, 3rd level visible, **4th level missing**
- ✅ Modal: All 4 levels visible

### After Fix
- ✅ Feed: All 4 levels visible
- ✅ Modal: All 4 levels visible (unchanged)

### Test Scenario

1. Create a comment thread with 4 levels:
   ```
   Top-level comment (Level 1)
   └─ Reply 1 (Level 2)
      └─ Reply 2 (Level 3)
         └─ Reply 3 (Level 4) ← This should now display!
   ```

2. Verify in **My Group Feed** (`/groups`):
   - All 4 levels display
   - Level 4 shows "No more replies allowed"

3. Verify in **Group Page** (`/group/{id}`):
   - All 4 levels display
   - Level 4 shows "No more replies allowed"

4. Verify in **Modal**:
   - All 4 levels display (should already work)
   - Level 4 shows "No more replies allowed"

## Performance Note

This fix adds one more level of `.Include()` to the EF Core query. This may slightly increase query complexity and result in the EF Core warning about multiple collection includes (which was already present). The impact should be minimal since we're only loading 3 comments per post in the feed.

## Related

- `MaxFeedPostCommentDepth` = 4 (configured in `appsettings.json`)
- `MultiLineCommentDisplay` correctly handles depth checking
- Recursive rendering in `GroupPostArticle.BuildCommentModelAsync` now works for all 4 levels
