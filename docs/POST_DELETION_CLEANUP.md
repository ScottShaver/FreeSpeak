# Post Deletion - Complete Cleanup Strategy

## Overview
When a post is deleted, all related data must be cleaned up from both the database and file system to prevent orphaned records and wasted storage space.

## Tables with Cascade Delete Configuration

The following tables have **automatic cascade delete** configured in `ApplicationDbContext.cs`:

### ✅ Automatically Cleaned (Cascade Delete)
1. **Comments** (`OnDelete(DeleteBehavior.Cascade)`)
   - All comments on the post
   - All replies to those comments (nested cascade)

2. **Likes** (`OnDelete(DeleteBehavior.Cascade)`)
   - All reactions/likes on the post

3. **CommentLikes** (`OnDelete(DeleteBehavior.Cascade)`)
   - All reactions/likes on comments (via comment cascade)

4. **PostImages** (`OnDelete(DeleteBehavior.Cascade)`)
   - All image database records

5. **PinnedPosts** (`OnDelete(DeleteBehavior.Cascade)`)
   - All pinned post records for this post

6. **PostNotificationMutes** (`OnDelete(DeleteBehavior.Cascade)`)
   - All notification mute records for this post

### ⚠️ Manually Cleaned in DeletePostAsync
The following items require manual cleanup because they don't have direct foreign key relationships or need file system operations:

1. **UserNotifications**
   - Notifications store PostId in JSON Data field (not a foreign key)
   - Cleaned via: `n.Data.Contains($"\"PostId\":{postId}")`
   - Includes: PostComment, PostLiked, CommentReply, CommentLiked notifications

2. **Physical Image Files**
   - Original image files in `AppData/uploads/posts/{userId}/images/`
   - Extracted from PostImage.ImageUrl format

3. **Cached Thumbnail Files**
   - Thumbnail sizes in `AppData/cache/resized-images/`
   - Patterns: `{filename}_thumbnail.jpg` and `{filename}_medium.jpg`

## DeletePostAsync Cleanup Order

The method performs deletions in this specific order to avoid foreign key violations:

```
1. PinnedPosts          - Remove pinned references
2. PostNotificationMutes - Remove mute records  
3. UserNotifications    - Remove notifications (JSON Data check)
4. CommentLikes         - Remove comment reactions
5. Comments & Replies   - Remove all comments
6. Likes                - Remove post reactions
7. PostImages (DB)      - Remove image records
8. Image Files          - Delete original files
9. Thumbnail Files      - Delete cached thumbnails
10. Post                - Finally delete the post itself
```

## File System Cleanup

### Original Images
**Location**: `AppData/uploads/posts/{userId}/images/{filename}`
- Extracted from: `/api/secure-files/post-image/{userId}/{imageId}/{filename}`
- Deleted: Synchronously during post deletion

### Cached Thumbnails
**Location**: `AppData/cache/resized-images/`
- `{fileNameWithoutExtension}_thumbnail.jpg` (150x150)
- `{fileNameWithoutExtension}_medium.jpg` (800x600)
- Deleted: Synchronously during post deletion

## Error Handling

### Database Errors
- All database operations wrapped in transaction
- Rollback occurs if any step fails
- Error logged and returned to caller

### File System Errors
- File deletion failures logged as warnings (not blocking)
- Continues with remaining deletions
- Original image deletion attempted before thumbnails
- Missing files silently skipped

## Logging

Each cleanup operation logs:
- **Info**: Count of deleted records/files
- **Warning**: Failed file deletions (non-blocking)
- **Error**: Fatal errors that prevent deletion

Example logs:
```
Deleted 5 pinned post record(s) for post 123
Deleted 2 notification mute record(s) for post 123
Deleted 8 notification(s) related to post 123
Deleted 15 comment like(s) for post 123
Deleted 10 comment(s) and their replies for post 123
Deleted 25 like(s) for post 123
Deleted 3 image record(s), 3 original file(s), and 6 cached thumbnail(s) for post 123
Post 123 and all related data deleted by user abc123
```

## Verification Checklist

When deleting a post, verify:
- ✅ Post removed from Posts table
- ✅ Comments and replies removed
- ✅ All likes and comment likes removed
- ✅ PinnedPosts entries removed
- ✅ PostNotificationMutes entries removed
- ✅ Related UserNotifications removed
- ✅ PostImages database records removed
- ✅ Original image files deleted from disk
- ✅ Cached thumbnail files deleted from disk

## Database Foreign Key Summary

```sql
-- Cascade Deletes (Automatic)
Comments.PostId         → Posts.Id           (CASCADE)
Comments.ParentCommentId → Comments.Id       (CASCADE)
Likes.PostId            → Posts.Id           (CASCADE)
CommentLikes.CommentId  → Comments.Id        (CASCADE)
PostImages.PostId       → Posts.Id           (CASCADE)
PinnedPosts.PostId      → Posts.Id           (CASCADE)
PostNotificationMutes.PostId → Posts.Id      (CASCADE)

-- Restrict Deletes (Manual Cleanup Required)
UserNotifications       → No FK (JSON Data field)
```

## Code Location

**Primary Implementation**: `FreeSpeakWeb/Services/PostService.cs`
- Method: `DeletePostAsync(int postId, string userId)`
- Lines: 230-381

**Database Configuration**: `FreeSpeakWeb/Data/ApplicationDbContext.cs`
- Method: `OnModelCreating(ModelBuilder modelBuilder)`
- Lines: 46-240

## Related Documentation

- Entity Relationships: `FreeSpeakWeb/Data/` entity classes
- Cascade Delete Configuration: `ApplicationDbContext.cs`
- Image Storage: `docs/IMAGE_STORAGE.md` (if exists)
- Notification System: `NOTIFICATIONS.md`

## Testing

### Manual Test Steps
1. Create a post with:
   - Multiple images
   - Several comments with replies
   - Multiple reactions
   - Pin the post
   - Mute notifications
2. Delete the post
3. Verify:
   - Database records removed
   - Files deleted from disk
   - Notifications removed
   - No orphaned data

### Unit Tests
See: `FreeSpeakWeb.Tests/Services/PostServiceTests.cs`
- Test: DeletePostAsync tests
