# Group Post System - Quick Reference Guide

## Table of Contents
- [Common Operations](#common-operations)
- [Service Methods](#service-methods)
- [Database Queries](#database-queries)
- [Validation Rules](#validation-rules)
- [Testing](#testing)

## Common Operations

### Creating a Group Post
```csharp
var (success, errorMessage, post) = await _groupPostService.CreateGroupPostAsync(
    groupId: 1,
    authorId: "user123",
    content: "Hello group!",
    imageUrls: new List<string> { "/path/to/image.jpg" }
);

if (success)
{
    // Post created successfully
    var postId = post.Id;
}
```

### Adding a Comment
```csharp
var (success, errorMessage, comment) = await _groupPostService.AddCommentAsync(
    postId: 1,
    authorId: "user123",
    content: "Great post!",
    imageUrl: null,  // Optional
    parentCommentId: null  // Null for top-level, ID for replies
);
```

### Liking a Post
```csharp
// Add a like
var (success, errorMessage) = await _groupPostService.LikePostAsync(
    postId: 1,
    userId: "user123",
    type: LikeType.Like  // or Love, Care, etc.
);

// Remove a like
var (success, errorMessage) = await _groupPostService.UnlikePostAsync(
    postId: 1,
    userId: "user123"
);
```

### Pinning a Post
```csharp
// Pin
var (success, errorMessage) = await _pinnedGroupPostService.PinGroupPostAsync(
    userId: "user123",
    postId: 1
);

// Unpin
var (success, errorMessage) = await _pinnedGroupPostService.UnpinGroupPostAsync(
    userId: "user123",
    postId: 1
);

// Check if pinned
var isPinned = await _pinnedGroupPostService.IsGroupPostPinnedAsync(
    userId: "user123",
    postId: 1
);
```

### Banning a User
```csharp
// Ban (requires admin or moderator)
var (success, errorMessage) = await _groupBannedMemberService.BanUserAsync(
    groupId: 1,
    userId: "baduser",
    bannedByUserId: "admin123"
);

// Unban
var (success, errorMessage) = await _groupBannedMemberService.UnbanUserAsync(
    groupId: 1,
    userId: "baduser",
    unbannedByUserId: "admin123"
);

// Check ban status
var isBanned = await _groupBannedMemberService.IsUserBannedAsync(
    groupId: 1,
    userId: "user123"
);
```

### Muting Notifications
```csharp
// Mute
var (success, errorMessage) = await _groupPostService.MutePostNotificationsAsync(
    postId: 1,
    userId: "user123"
);

// Unmute
var (success, errorMessage) = await _groupPostService.UnmutePostNotificationsAsync(
    postId: 1,
    userId: "user123"
);

// Check mute status
var isMuted = await _groupPostService.IsPostNotificationMutedAsync(
    postId: 1,
    userId: "user123"
);
```

## Service Methods

### GroupPostService

#### Post Operations
| Method | Purpose | Returns |
|--------|---------|---------|
| `CreateGroupPostAsync` | Create new post | `(bool, string?, GroupPost?)` |
| `UpdateGroupPostAsync` | Update content/images | `(bool, string?, List<GroupPostImage>?)` |
| `DeleteGroupPostAsync` | Delete post | `(bool, string?)` |
| `GetGroupPostByIdAsync` | Get single post | `GroupPost?` |
| `GetGroupPostsAsync` | Get paginated posts | `List<GroupPost>` |
| `GetUserGroupPostsAsync` | Get user's posts in group | `List<GroupPost>` |
| `GetGroupPostCountAsync` | Get total count | `int` |

#### Comment Operations
| Method | Purpose | Returns |
|--------|---------|---------|
| `AddCommentAsync` | Add comment/reply | `(bool, string?, GroupPostComment?)` |
| `DeleteCommentAsync` | Delete comment | `(bool, string?)` |
| `GetCommentsAsync` | Get with nested replies | `List<GroupPostComment>` |

#### Like Operations
| Method | Purpose | Returns |
|--------|---------|---------|
| `LikePostAsync` | Add/update like | `(bool, string?)` |
| `UnlikePostAsync` | Remove like | `(bool, string?)` |
| `GetUserLikeAsync` | Check like status | `GroupPostLike?` |
| `LikeCommentAsync` | Add/update comment like | `(bool, string?)` |
| `UnlikeCommentAsync` | Remove comment like | `(bool, string?)` |
| `GetUserCommentLikeAsync` | Check comment like | `GroupPostCommentLike?` |

#### Notification Mute Operations
| Method | Purpose | Returns |
|--------|---------|---------|
| `MutePostNotificationsAsync` | Mute notifications | `(bool, string?)` |
| `UnmutePostNotificationsAsync` | Unmute notifications | `(bool, string?)` |
| `IsPostNotificationMutedAsync` | Check mute status | `bool` |

### PinnedGroupPostService
| Method | Purpose | Returns |
|--------|---------|---------|
| `PinGroupPostAsync` | Pin post | `(bool, string?)` |
| `UnpinGroupPostAsync` | Unpin post | `(bool, string?)` |
| `IsGroupPostPinnedAsync` | Check if pinned | `bool` |
| `GetPinnedGroupPostsAsync` | Get all pinned | `List<GroupPost>` |
| `GetPinnedGroupPostsByGroupAsync` | Get pinned in group | `List<GroupPost>` |
| `GetPinnedGroupPostCountAsync` | Count pinned | `int` |

### GroupBannedMemberService
| Method | Purpose | Returns |
|--------|---------|---------|
| `BanUserAsync` | Ban user | `(bool, string?)` |
| `UnbanUserAsync` | Unban user | `(bool, string?)` |
| `IsUserBannedAsync` | Check ban status | `bool` |
| `GetBannedMembersAsync` | Get banned list | `List<GroupBannedMember>` |
| `GetBannedMemberCountAsync` | Count banned | `int` |
| `GetUserBansAsync` | Get user's bans | `List<GroupBannedMember>` |

## Database Queries

### Get Posts with Full Details
```csharp
using var context = await _contextFactory.CreateDbContextAsync();

var posts = await context.GroupPosts
    .Include(p => p.Author)
    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
    .Include(p => p.Group)
    .Where(p => p.GroupId == groupId)
    .OrderByDescending(p => p.CreatedAt)
    .Skip(skip)
    .Take(take)
    .ToListAsync();
```

### Get Comments with Nested Replies
```csharp
var comments = await context.GroupPostComments
    .Include(c => c.Author)
    .Include(c => c.Replies)
        .ThenInclude(r => r.Author)
    .Where(c => c.PostId == postId && c.ParentCommentId == null)
    .OrderBy(c => c.CreatedAt)
    .ToListAsync();
```

### Check Multiple Permissions
```csharp
// Check membership and ban status
var isMember = await context.GroupUsers
    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

var isBanned = await context.GroupBannedMembers
    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == userId);

var isAdminOrMod = await context.GroupUsers
    .AnyAsync(gu => gu.GroupId == groupId && 
                   gu.UserId == userId && 
                   (gu.IsAdmin || gu.IsModerator));
```

## Validation Rules

### Access Requirements
| Operation | Requires |
|-----------|----------|
| Create Post | Group membership, Not banned |
| Update Post | Author OR (Admin/Moderator) |
| Delete Post | Author OR (Admin/Moderator) |
| Add Comment | Group membership, Not banned |
| Delete Comment | Author OR (Admin/Moderator) |
| Like Post/Comment | Group membership, Not banned |
| Pin Post | Group membership |
| Ban User | Admin OR Moderator |
| Unban User | Admin OR Moderator |

### Ban Rules
- ❌ Cannot ban group creator
- ❌ Moderators cannot ban admins
- ❌ Cannot ban already banned users
- ✅ Banning removes group membership
- ✅ Unbanning allows re-joining

### Data Constraints
- One like per user per post (unique constraint)
- One like per user per comment (unique constraint)
- One pin per user per post (unique constraint)
- One ban per user per group (unique constraint)
- One mute per user per post (unique constraint)

## Testing

### Using TestDataFactory
```csharp
// Create test entities
var user = TestDataFactory.CreateTestUser(id: "user1");
var group = TestDataFactory.CreateTestGroup("creator1", "Test Group");
var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Test content");
var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1", "Test comment");
var like = TestDataFactory.CreateTestGroupPostLike(1, "user1");
var pin = TestDataFactory.CreateTestPinnedGroupPost("user1", 1);
var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");
var mute = TestDataFactory.CreateTestGroupPostNotificationMute(1, "user1");
```

### Test Pattern
```csharp
[Fact]
public async Task OperationName_Scenario_ExpectedOutcome()
{
    // Arrange
    var dbFactory = CreateDbContextFactory("TestName");
    var logger = CreateMockLogger<ServiceName>();
    var service = new ServiceName(dbFactory, logger);
    
    // Set up test data
    using (var context = await dbFactory.CreateDbContextAsync())
    {
        // Add entities
        await context.SaveChangesAsync();
    }
    
    // Act
    var (success, errorMessage) = await service.MethodAsync(...);
    
    // Assert
    success.Should().BeTrue();
    errorMessage.Should().BeNull();
    
    // Verify database state
    using (var context = await dbFactory.CreateDbContextAsync())
    {
        // Check results
    }
}
```

### Common Test Scenarios
1. ✅ Valid operation succeeds
2. ✅ Non-member rejected
3. ✅ Banned user rejected
4. ✅ Permission validation
5. ✅ Duplicate action rejected
6. ✅ Cascade deletes work
7. ✅ Counts updated correctly
8. ✅ Proper error messages

## Error Handling

### Common Error Messages
| Error | Meaning |
|-------|---------|
| "Post not found." | Invalid post ID |
| "You must be a member of the group to [action]." | Not a group member |
| "You are banned from this group." | User is banned |
| "You are not authorized to [action]." | Insufficient permissions |
| "Post is already pinned." | Duplicate pin attempt |
| "Post is not pinned." | Unpin non-pinned post |
| "User is already banned from this group." | Duplicate ban |
| "User is not banned from this group." | Invalid unban |
| "Cannot ban the group creator." | Attempt to ban creator |
| "Moderators cannot ban administrators." | Permission violation |

## Performance Tips

1. **Use Include() for relationships**: Avoid n+1 queries
2. **Leverage cached counts**: Use `LikeCount`/`CommentCount` instead of COUNT()
3. **Paginate results**: Always use skip/take
4. **Index foreign keys**: Already done, but good to know
5. **Batch operations**: When possible, group database operations

## Common Patterns

### Checking Multiple Conditions
```csharp
// Check if user can perform action
var canPerform = await context.GroupUsers
    .Where(gu => gu.GroupId == groupId && gu.UserId == userId)
    .Select(gu => new
    {
        IsMember = true,
        IsBanned = context.GroupBannedMembers
            .Any(gbm => gbm.GroupId == groupId && gbm.UserId == userId),
        IsAdmin = gu.IsAdmin,
        IsModerator = gu.IsModerator
    })
    .FirstOrDefaultAsync();

if (canPerform == null) return (false, "Not a member");
if (canPerform.IsBanned) return (false, "Banned");
```

### Updating Cached Counts
```csharp
// After adding a like
post.LikeCount++;
await context.SaveChangesAsync();

// After removing a like
post.LikeCount--;
await context.SaveChangesAsync();

// After adding a comment (including nested)
post.CommentCount++;
await context.SaveChangesAsync();

// After deleting a comment (including children)
var commentCount = 1 + comment.Replies.Count;
post.CommentCount -= commentCount;
await context.SaveChangesAsync();
```

## Additional Resources

- Full Documentation: `docs/GROUP_POST_SYSTEM.md`
- Implementation Summary: `docs/GROUP_POST_IMPLEMENTATION_SUMMARY.md`
- Test Examples: `FreeSpeakWeb.Tests/Services/GroupPostServiceTests.cs`
- Change History: `CHANGELOG.md`
