# Post vs GroupPost System Analysis
## Comprehensive Comparison and Refactoring Recommendations

**Date:** 2026  
**Project:** FreeSpeak  
**Analysis Scope:** Post and GroupPost systems (Data Models, Services, UI Components)

---

## Executive Summary

The Post and GroupPost systems are **highly duplicative** with nearly identical functionality implemented twice. The analysis reveals:

- **Duplication Level:** ~80% code duplication across services and UI components
- **Missing Features:** GroupPostService lacks several critical features present in PostService
- **Inconsistencies:** Different handling of image deletion, notifications, and retrieval methods
- **Refactoring Potential:** High - could reduce codebase by 30-40% through abstraction

---

## 1. Data Model Comparison

### 1.1 Post vs GroupPost

| Feature | Post | GroupPost | Notes |
|---------|------|-----------|-------|
| Id | ✅ | ✅ | Identical |
| AuthorId | ✅ | ✅ | Identical |
| Content | ✅ | ✅ | Identical |
| CreatedAt | ✅ | ✅ | Identical |
| UpdatedAt | ✅ | ✅ | Identical |
| LikeCount | ✅ | ✅ | Identical |
| CommentCount | ✅ | ✅ | Identical |
| ShareCount | ✅ | ✅ | Identical |
| GroupId | ❌ | ✅ | GroupPost specific |
| AudienceType | ✅ | ❌ | Post specific |
| Images Collection | ✅ | ✅ | Different entity types |
| Likes Collection | ✅ | ✅ | Different entity types |
| Comments Collection | ✅ | ✅ | Different entity types |

**Finding:** Models are 90% identical. Only differences are GroupId and AudienceType.

### 1.2 Comment vs GroupPostComment

| Feature | Comment | GroupPostComment |
|---------|---------|------------------|
| Structure | Identical | Identical |
| Properties | Identical | Identical |
| Relationships | Identical | Identical |

**Finding:** 100% duplicated. Only difference is the Post relationship type.

### 1.3 Like vs GroupPostLike

| Feature | Like | GroupPostLike |
|---------|------|---------------|
| Structure | Identical | Identical |
| Properties | Identical | Identical |
| Type Enum | LikeType | LikeType (same) |

**Finding:** 100% duplicated. Only difference is the Post relationship type.

### 1.4 CommentLike vs GroupPostCommentLike

**Finding:** 100% duplicated structures.

---

## 2. Service Layer Comparison

### 2.1 PostService (1959 lines) vs GroupPostService (1311 lines)

#### Features Present in PostService but Missing in GroupPostService:

1. **Image Management**
   - `AddImageToPostAsync()` - missing
   - `RemoveImageFromPostAsync()` - missing
   - `GetPostImagesAsync()` - missing

2. **Comment Retrieval Variations**
   - `GetCommentsAsync()` (non-paged) - missing
   - `GetLastCommentsAsync()` (for feed display) - **critical for feed UI**

3. **Post Retrieval Methods**
   - `GetPostByIdAsync()` with full includes
   - `GetFeedPostsAsync()` with friendship filtering
   - `GetPostsByUserAsync()`
   - `GetPublicPostsAsync()` for unauthenticated users
   - `GetFeedPostsCountAsync()`
   - `GetPublicPostByIdAsync()` with PostViewModel building

4. **Like Operations**
   - `ToggleLikeAsync()` - simpler like/unlike
   - `HasUserLikedPostAsync()` - boolean check
   - `GetPostLikesAsync()` - list of users
   - `GetLikeCountAsync()` - separate count method
   - `RemoveLikeAsync()` - dedicated unlike method

5. **User Uploads**
   - `GetUserUploadedImagesAsync()` - paginated image gallery
   - `GetUserUploadedVideosAsync()` - paginated video gallery

6. **Pinned Posts**
   - `IsPostPinnedAsync()`
   - `PinPostAsync()`
   - `UnpinPostAsync()`
   - `GetPinnedStatusForPostsAsync()`
   - `GetPinnedPostsAsync()`
   - **Note:** GroupPost has separate `PinnedGroupPostService`

7. **Audience Management**
   - `UpdatePostAudienceAsync()` - change post visibility
   - Friendship-based feed filtering

#### Features in GroupPostService with Additional Logic:

1. **Group Membership Validation**
   - Every operation checks group membership
   - Ban status checking
   - Admin/Moderator permissions for delete

2. **Group Statistics**
   - `GetTotalPostCountAsync()`
   - `GetPostCountSinceAsync()`
   - `GetLastActivityAsync()`

3. **User Posts in Group**
   - `GetUserGroupPostsAsync()` - posts by specific user in group
   - `GetAllGroupPostsForUserAsync()` - all group posts for user's groups

### 2.2 Critical Differences in Implementation

#### DeletePostAsync vs DeleteGroupPostAsync

**PostService.DeletePostAsync() (Lines 230-377):**
```csharp
// Comprehensive cleanup including:
- Pinned post records
- Notification mutes  
- Related notifications (searches Data field)
- Comment likes (explicit deletion)
- All comments and replies
- All post likes
- All images with PHYSICAL FILE DELETION
  - Original image files
  - Cached thumbnails (thumbnail and medium sizes)
- Post entity
```

**GroupPostService.DeleteGroupPostAsync() (Lines 200-254):**
```csharp
// Minimal cleanup:
- Pinned group post records
- Post entity (relies on cascade for images)
- NO notification cleanup
- NO physical file deletion
- NO comment/like explicit cleanup
```

**Issue:** GroupPostService leaves orphaned files and notifications. This is a **critical bug**.

#### AddCommentAsync - Notification Logic

Both services have **nearly identical** notification logic (~50 lines each):
- Check if commenting on own post/comment
- Format user display name
- Create notification with same data structure
- Only difference: `NotificationType.PostComment` vs `NotificationType.GroupPostComment`

**Refactoring Opportunity:** Extract to shared helper method.

#### Reaction Methods

`AddOrUpdateReactionAsync()` and `AddOrUpdateCommentReactionAsync()` are **duplicated** across both services with only entity type differences.

**Refactoring Opportunity:** Generic reaction handling.

---

## 3. UI Component Comparison

### 3.1 PostDetailModal vs GroupPostDetailModal

**Similarities (95% identical):**
- Modal backdrop and container structure
- Close button implementation
- Comment loading (initial + pagination)
- `LoadMoreCommentsAsync()` JS interop pattern
- Comment scroll initialization
- Target comment highlighting
- `GetInitials()` helper method (100% duplicate)
- `BuildCommentDisplayModel()` recursive method (100% duplicate)
- `FindCommentById()` recursive search (100% duplicate)
- Comment editor at bottom
- Direct comment count limiting

**Differences:**
```diff
PostDetailModal:
- @inject PostService PostService
- Parameters: PostId, AuthorId, AudienceType, IsPinned
- Uses Comment entity

GroupPostDetailModal:
- @inject GroupPostService GroupPostService
+ Parameters: PostId, GroupId, GroupName, AuthorId, IsPinned
+ Additional: OnEditPost callback
+ Local state: localCommentCount, localLikeCount (track in modal)
- Uses GroupPostComment entity
```

**Refactoring Opportunity:** Create base component with generic service interface.

### 3.2 FeedArticle vs GroupPostArticle

**Lines:** FeedArticle (1098) vs GroupPostArticle (803)

**Expected Pattern:** Similar duplication as modal components.

---

## 4. Identified Redundancies Within Each System

### 4.1 PostService Internal Redundancies

1. **Reaction Handling Duplication:**
   - `AddOrUpdateReactionAsync()` - for posts
   - `AddOrUpdateCommentReactionAsync()` - for comments
   - Nearly identical logic, different entities

2. **Notification Creation Pattern:**
   - Repeated pattern across 5+ methods:
     ```csharp
     var reactor = await context.Users.FindAsync(userId);
     var formattedName = await _userPreferenceService.FormatUserDisplayNameAsync(...);
     var message = $"<strong>{formattedName}</strong> [action]";
     await _notificationService.CreateNotificationAsync(...);
     ```

3. **Mute Checking Pattern:**
   - Repeated in `AddCommentAsync()` and `AddOrUpdateReactionAsync()`
   - Could extract to helper method

4. **Image File Path Parsing:**
   - In `DeletePostAsync()`: URL parsing logic for file deletion
   - Could be utility method

### 4.2 GroupPostService Internal Redundancies

1. **Group Access Validation:**
   - Repeated in every method:
     ```csharp
     var isMember = await context.GroupUsers.AnyAsync(...);
     var isBanned = await context.GroupBannedMembers.AnyAsync(...);
     ```
   - Should be extracted to `ValidateGroupAccessAsync()`

2. **Admin/Moderator Check:**
   - Duplicated in `DeleteGroupPostAsync()` and `DeleteCommentAsync()`
   - Extract to `IsGroupAdminOrModeratorAsync()`

3. **Reaction Methods:**
   - Same duplication as PostService

4. **Notification Patterns:**
   - Same duplication as PostService

---

## 5. Missing Features / Inconsistencies

### 5.1 GroupPostService Missing Features

| Feature | Priority | Impact |
|---------|----------|--------|
| Physical file deletion in delete operations | **CRITICAL** | Disk space leak |
| Notification cleanup on delete | **HIGH** | Database bloat, orphaned data |
| `GetLastCommentsAsync()` for feed | **HIGH** | Feed UI cannot show preview comments |
| Image management methods | **MEDIUM** | Can't manage images post-creation |
| `RemoveLikeAsync()` method | **LOW** | Has UnlikePostAsync instead |
| User uploads retrieval | **LOW** | Feature not needed for groups? |

### 5.2 PostService vs GroupPostService Inconsistencies

1. **Like Method Names:**
   - Post: `AddOrUpdateReactionAsync()` + `RemoveLikeAsync()`
   - GroupPost: `LikePostAsync()` + `UnlikePostAsync()`
   - **Issue:** Inconsistent API

2. **Delete Authorization:**
   - Post: Only author can delete
   - GroupPost: Author OR admin/moderator can delete
   - **Issue:** Inconsistent permission model (but may be intentional)

3. **Comment Limit Checking:**
   - Post: Checks `MaxFeedPostDirectCommentCount` in service
   - GroupPost: No limit checking in service (UI only?)
   - **Issue:** Business logic inconsistency

### 5.3 Comment System Inconsistency

**PostService** has:
- `GetCommentsAsync()` - all top-level comments
- `GetCommentsPagedAsync()` - paginated
- `GetLastCommentsAsync()` - last N for feed display

**GroupPostService** has:
- `GetCommentsAsync()` - all top-level comments
- `GetCommentsPagedAsync()` - paginated
- ❌ Missing `GetLastCommentsAsync()` - **prevents feed comment preview**

---

## 6. Refactoring Recommendations

### 6.1 Short-Term Fixes (High Priority)

#### 1. Fix GroupPostService.DeleteGroupPostAsync()
**Priority:** CRITICAL

Add to `DeleteGroupPostAsync()`:
```csharp
// Delete all group post notification mute records
var mutedNotifications = await context.GroupPostNotificationMutes
    .Where(m => m.PostId == postId)
    .ToListAsync();

// Delete all related notifications
var relatedNotifications = await context.UserNotifications
    .Where(n => n.Data != null && n.Data.Contains($"\"GroupPostId\":{postId}"))
    .ToListAsync();

// Delete all comment likes first
var commentIds = post.Comments.Select(c => c.Id).ToList();
var commentLikes = await context.GroupPostCommentLikes
    .Where(cl => commentIds.Contains(cl.CommentId))
    .ToListAsync();

// Delete all images with PHYSICAL FILE DELETION
foreach (var postImage in post.Images)
{
    // Parse ImageUrl and delete files
    // Delete cached thumbnails
}
```

#### 2. Add GetLastCommentsAsync() to GroupPostService
**Priority:** HIGH

Copy implementation from PostService:
```csharp
public async Task<List<GroupPostComment>> GetLastCommentsAsync(int postId, int count)
{
    // Implementation from PostService lines 944-964
}
```

#### 3. Standardize Like Method Names
**Priority:** MEDIUM

Choose one pattern and apply consistently:
- Option A: `AddOrUpdateReactionAsync()` + `RemoveReactionAsync()`
- Option B: `LikePostAsync()` + `UnlikePostAsync()`

Recommended: **Option A** (more explicit about reaction vs simple like)

### 6.2 Medium-Term Refactoring

#### 1. Extract Shared Service Layer

Create `PostServiceBase<TPost, TComment, TLike>` abstract class:
```csharp
public abstract class PostServiceBase<TPost, TComment, TLike, TCommentLike>
    where TPost : class
    where TComment : class
    where TLike : class
    where TCommentLike : class
{
    // Common methods:
    // - AddCommentAsync (with abstract group validation hook)
    // - AddOrUpdateReactionAsync
    // - AddOrUpdateCommentReactionAsync
    // - Notification creation helpers
    // - Reaction breakdown methods
    // - Comment retrieval methods
}

public class PostService : PostServiceBase<Post, Comment, Like, CommentLike>
{
    // Post-specific: audience handling, friendship filtering
}

public class GroupPostService : PostServiceBase<GroupPost, GroupPostComment, GroupPostLike, GroupPostCommentLike>
{
    // GroupPost-specific: group validation, admin checks
}
```

**Benefits:**
- Reduce code from ~3270 lines to ~2000 lines
- Single source of truth for shared logic
- Easier to maintain and test

#### 2. Extract Notification Helper Service

Create `PostNotificationHelper`:
```csharp
public class PostNotificationHelper
{
    public async Task NotifyPostReaction(string postAuthorId, string reactorId, int postId, LikeType reactionType, NotificationType notificationType);
    public async Task NotifyComment(string postAuthorId, string commenterId, int postId, int commentId, NotificationType notificationType);
    public async Task NotifyCommentReply(string parentCommentAuthorId, string replierId, int postId, int commentId, NotificationType notificationType);
    public async Task NotifyCommentReaction(string commentAuthorId, string reactorId, int postId, int commentId, LikeType reactionType, NotificationType notificationType);
}
```

**Benefits:**
- Eliminate ~200 lines of duplicated notification code
- Centralize notification logic
- Easier to modify notification templates

#### 3. Extract Group Validation Helper

Create `GroupAccessValidator`:
```csharp
public class GroupAccessValidator
{
    public async Task<(bool IsMember, bool IsBanned)> ValidateUserAccessAsync(int groupId, string userId);
    public async Task<bool> IsGroupAdminOrModeratorAsync(int groupId, string userId);
    public async Task<(bool Success, string? ErrorMessage)> ValidateUserCanPostAsync(int groupId, string userId);
}
```

**Benefits:**
- Eliminate duplication across GroupPostService methods
- Consistent access control
- Easier to modify permission logic

### 6.3 Long-Term Architecture Improvement

#### 1. Unified Post System with Context

Instead of separate Post and GroupPost entities, use a single Post entity:

```csharp
public class Post
{
    public int Id { get; set; }
    public required string AuthorId { get; set; }
    public required string Content { get; set; }
    
    // Context - either group or user feed
    public PostContext Context { get; set; } = PostContext.UserFeed;
    public int? GroupId { get; set; } // Null for user feed posts
    
    public AudienceType AudienceType { get; set; } // Ignored for group posts
    
    // ... rest of properties
}

public enum PostContext
{
    UserFeed,
    Group
}
```

**Benefits:**
- Single codebase for all post operations
- No duplication of Comment, Like, etc.
- Easier to add new post contexts (events, pages, etc.)

**Drawbacks:**
- Requires database migration
- Significant refactoring effort
- Risk of regression bugs

#### 2. Repository Pattern Implementation

Create generic repositories:
```csharp
public interface IPostRepository<TPost>
{
    Task<TPost?> GetByIdAsync(int id);
    Task<List<TPost>> GetPagedAsync(int skip, int take);
    Task<bool> DeleteAsync(int id, string userId);
    // ... etc
}

public class PostRepository : IPostRepository<Post> { }
public class GroupPostRepository : IPostRepository<GroupPost> { }
```

**Benefits:**
- Cleaner service layer
- Easier unit testing
- Database abstraction

### 6.4 UI Component Refactoring

#### 1. Create Generic DetailModal Component

```razor
<DetailModalBase TService="PostService" TComment="Comment">
    @* Shared modal structure *@
</DetailModalBase>
```

**Benefits:**
- Eliminate ~80% duplication between modals
- Single source for modal behavior
- Extract common methods (GetInitials, BuildCommentDisplayModel, FindCommentById)

#### 2. Extract Shared Helper Components

Create:
- `CommentHelpers.cs` - GetInitials(), BuildCommentDisplayModel()
- `CommentListComponent.razor` - Comment display and pagination
- `CommentEditorComponent.razor` - Comment input (already exists as MultiLineCommentEditor)

---

## 7. Data Model Improvement Suggestions

### 7.1 Add Base Classes

```csharp
public abstract class PostBase
{
    public int Id { get; set; }
    public required string AuthorId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int LikeCount { get; set; } = 0;
    public int CommentCount { get; set; } = 0;
    public int ShareCount { get; set; } = 0;
}

public class Post : PostBase
{
    public AudienceType AudienceType { get; set; }
    // Post-specific collections
}

public class GroupPost : PostBase
{
    public required int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    // GroupPost-specific collections
}
```

**Benefits:**
- Explicit shared structure
- Enables generic constraints
- Self-documenting code

### 7.2 Consider Interface-Based Approach

```csharp
public interface IPost
{
    int Id { get; set; }
    string AuthorId { get; set; }
    string Content { get; set; }
    DateTime CreatedAt { get; set; }
    // ... common properties
}

public interface ILikeable
{
    int LikeCount { get; set; }
    ICollection<ILike> Likes { get; set; }
}

public interface ICommentable
{
    int CommentCount { get; set; }
}
```

**Benefits:**
- Flexible composition
- Enables polymorphism
- Better for future extensions

---

## 8. Testing Recommendations

### 8.1 Current State

Based on file search:
- `PostServiceTests.cs` - exists
- `PostServiceEdgeCaseTests.cs` - exists
- `PostServiceIntegrationTests.cs` - exists
- `GroupPostServiceTests.cs` - exists
- `PinnedGroupPostServiceTests.cs` - exists

### 8.2 Gaps to Address

1. **No Parity Tests:**
   - Test that Post and GroupPost operations produce equivalent results
   - Ensure notification types are created correctly for both

2. **No Delete Operation Tests:**
   - Critical: Test GroupPostService delete actually cleans up files
   - Test notification cleanup

3. **No Permission Tests:**
   - Test group admin/moderator delete permissions
   - Test ban status blocking

### 8.3 Recommended Test Suite

Create `PostGroupPostParityTests.cs`:
```csharp
[Theory]
[InlineData("Post", typeof(PostService))]
[InlineData("GroupPost", typeof(GroupPostService))]
public async Task CommentOperation_ProducesEquivalentResults(string type, Type serviceType)
{
    // Verify both systems handle comments identically
}

[Fact]
public async Task GroupPostDelete_RemovesPhysicalFiles()
{
    // CRITICAL: Verify file cleanup
}

[Fact]
public async Task GroupPostDelete_RemovesNotifications()
{
    // CRITICAL: Verify notification cleanup
}
```

---

## 9. Implementation Priority Matrix

| Task | Priority | Effort | Risk | Impact |
|------|----------|--------|------|--------|
| Fix GroupPost delete (files + notifications) | P0 | Small | Low | High |
| Add GetLastCommentsAsync to GroupPostService | P1 | Trivial | Low | Medium |
| Standardize method naming | P1 | Small | Low | Medium |
| Extract NotificationHelper | P2 | Medium | Low | High |
| Extract GroupAccessValidator | P2 | Small | Low | Medium |
| Create PostServiceBase | P2 | Large | Medium | Very High |
| Refactor UI modals | P3 | Medium | Medium | Medium |
| Unified Post architecture | P4 | Very Large | High | Very High |
| Repository pattern | P4 | Large | Medium | Medium |

### Priority Definitions
- **P0:** Critical bug fix - do immediately
- **P1:** High priority - next sprint
- **P2:** Medium priority - within 2 sprints
- **P3:** Low priority - future enhancement
- **P4:** Long-term architectural - plan for major version

---

## 10. Estimated Impact

### Code Reduction Potential

**Current State:**
- PostService: 1959 lines
- GroupPostService: 1311 lines
- PostDetailModal: ~540 lines
- GroupPostDetailModal: ~540 lines
- Data models: ~600 lines (duplicated entities)
- **Total:** ~5000 lines of duplicated/related code

**After Short-Term Fixes:** ~4950 lines (1% reduction)
- Fixes critical bugs but minimal code reduction

**After Medium-Term Refactoring:** ~3200 lines (36% reduction)
- PostServiceBase: ~800 lines
- PostService: ~400 lines (post-specific)
- GroupPostService: ~400 lines (group-specific)
- NotificationHelper: ~200 lines
- GroupAccessValidator: ~100 lines
- UI components: ~800 lines (shared + specific)
- Data models: ~500 lines

**After Long-Term Architecture:** ~2500 lines (50% reduction)
- Unified Post system
- Generic repositories
- Shared UI components

### Maintenance Impact

**Current:**
- Bug fix requires changes in 2+ places
- New feature requires duplicate implementation
- High risk of divergence between systems
- Test coverage must be duplicated

**After Refactoring:**
- Bug fix in one place
- New feature single implementation
- Guaranteed consistency
- Shared test suite

---

## 11. Migration Path

### Phase 1: Critical Fixes (1-2 days)
1. Fix GroupPostService.DeleteGroupPostAsync()
2. Add GetLastCommentsAsync()
3. Write tests for both
4. Deploy as hotfix

### Phase 2: Code Quality (1 week)
1. Standardize method names
2. Extract NotificationHelper
3. Extract GroupAccessValidator
4. Update existing tests
5. Deploy as minor version

### Phase 3: Architecture (2-3 weeks)
1. Create PostServiceBase
2. Refactor PostService to inherit
3. Refactor GroupPostService to inherit
4. Comprehensive testing
5. Deploy as minor version

### Phase 4: UI Refactoring (1-2 weeks)
1. Create shared modal base
2. Extract helper components
3. Update existing modals
4. Visual regression testing
5. Deploy as minor version

### Phase 5: Unified Architecture (Major version - 4-6 weeks)
1. Design unified schema
2. Create migration strategy
3. Implement new models
4. Gradual migration of data
5. Extensive testing
6. Deploy as major version

---

## 12. Risks and Mitigation

### Risk 1: Breaking Changes During Refactoring
**Mitigation:**
- Comprehensive unit tests before refactoring
- Integration tests for all critical paths
- Feature flags for gradual rollout
- Parallel implementations during transition

### Risk 2: Database Migration Failures (Long-term)
**Mitigation:**
- Thorough migration testing in staging
- Rollback scripts prepared
- Blue-green deployment strategy
- Data validation scripts

### Risk 3: Performance Regression
**Mitigation:**
- Benchmark current performance
- Performance tests in CI/CD
- Query optimization review
- Caching strategy

### Risk 4: UI Regression
**Mitigation:**
- Visual regression testing
- Manual QA pass
- Gradual rollout
- A/B testing if needed

---

## 13. Conclusion

The Post and GroupPost systems exhibit **significant duplication** with **critical bugs** in the GroupPost implementation. 

**Immediate Actions Required:**
1. ✅ Fix file deletion in GroupPostService.DeleteGroupPostAsync()
2. ✅ Fix notification cleanup in GroupPostService.DeleteGroupPostAsync()
3. ✅ Add GetLastCommentsAsync() to GroupPostService

**Recommended Refactoring:**
1. Extract shared business logic to PostServiceBase
2. Create helper services for notifications and group validation
3. Standardize API naming conventions
4. Refactor UI components to use shared base

**Long-Term Vision:**
- Consider unified Post architecture with context-aware behavior
- Implement repository pattern for cleaner separation
- Generic UI components for all post types

**Expected Outcomes:**
- 36-50% code reduction
- Improved maintainability
- Guaranteed feature parity
- Reduced bug surface area
- Faster feature development

---

## Appendix A: Detailed Method Comparison

### PostService Methods (59 total)

**Post Operations (8):**
- CreatePostAsync
- UpdatePostAsync
- UpdatePostAudienceAsync
- DeletePostAsync
- GetPostByIdAsync
- GetFeedPostsAsync
- GetPostsByUserAsync
- GetPublicPostsAsync ✅
- GetFeedPostsCountAsync
- GetPublicPostByIdAsync ✅

**Comment Operations (9):**
- AddCommentAsync
- DeleteCommentAsync
- GetCommentsAsync
- GetCommentsPagedAsync
- GetLastCommentsAsync ✅
- GetDirectCommentCountAsync
- GetRepliesAsync
- BuildCommentDisplayModelAsync (private helper)

**Like Operations (10):**
- ToggleLikeAsync ✅
- HasUserLikedPostAsync ✅
- GetPostLikesAsync ✅
- GetLikeCountAsync ✅
- AddOrUpdateReactionAsync
- GetReactionBreakdownAsync
- GetUserReactionAsync
- RemoveLikeAsync ✅
- GetPostLikesWithDetailsAsync

**Comment Like Operations (6):**
- AddOrUpdateCommentReactionAsync
- RemoveCommentReactionAsync
- GetCommentReactionBreakdownAsync
- GetUserCommentReactionAsync
- GetCommentLikeCountAsync

**Image Operations (3):**
- AddImageToPostAsync ✅
- RemoveImageFromPostAsync ✅
- GetPostImagesAsync ✅

**Pinned Posts (5):**
- IsPostPinnedAsync ✅
- PinPostAsync ✅
- UnpinPostAsync ✅
- GetPinnedStatusForPostsAsync ✅
- GetPinnedPostsAsync ✅

**Notification Muting (3):**
- MutePostNotificationsAsync
- UnmutePostNotificationsAsync
- IsPostNotificationMutedAsync

**User Uploads (2):**
- GetUserUploadedImagesAsync ✅
- GetUserUploadedVideosAsync ✅

✅ = Not present in GroupPostService

### GroupPostService Methods (40 total)

**Post Operations (6):**
- CreateGroupPostAsync
- UpdateGroupPostAsync
- DeleteGroupPostAsync (⚠️ incomplete)
- GetGroupPostByIdAsync
- GetGroupPostsAsync
- GetUserGroupPostsAsync
- GetGroupPostCountAsync
- GetAllGroupPostsForUserAsync

**Comment Operations (7):**
- AddCommentAsync
- DeleteCommentAsync
- GetCommentsAsync
- GetCommentsPagedAsync
- GetDirectCommentCountAsync
- GetRepliesAsync

**Like Operations (4):**
- LikePostAsync
- UnlikePostAsync
- GetUserLikeAsync
- GetReactionBreakdownAsync
- GetUserReactionAsync
- GetGroupPostLikesWithDetailsAsync

**Comment Like Operations (6):**
- LikeCommentAsync
- UnlikeCommentAsync
- GetUserCommentLikeAsync
- GetCommentLikeCountAsync
- GetCommentReactionBreakdownAsync
- GetUserCommentReactionAsync

**Notification Muting (3):**
- MutePostNotificationsAsync
- UnmutePostNotificationsAsync
- IsPostNotificationMutedAsync

**Group Statistics (3):**
- GetTotalPostCountAsync
- GetPostCountSinceAsync
- GetLastActivityAsync

⚠️ = Has critical issues

### Method Name Inconsistencies

| PostService | GroupPostService | Should Be |
|-------------|------------------|-----------|
| AddOrUpdateReactionAsync | LikePostAsync | AddOrUpdateReactionAsync |
| RemoveLikeAsync | UnlikePostAsync | RemoveReactionAsync |
| GetPostLikesAsync | - | GetPostLikesAsync |
| HasUserLikedPostAsync | - | HasUserLikedPostAsync |

---

## Appendix B: Notification Type Mapping

| Action | PostService | GroupPostService |
|--------|-------------|------------------|
| Post Liked | NotificationType.PostLiked | NotificationType.GroupPostLiked |
| Post Commented | NotificationType.PostComment | NotificationType.GroupPostComment |
| Comment Replied | NotificationType.CommentReply | NotificationType.GroupCommentReply |
| Comment Liked | NotificationType.CommentLiked | NotificationType.GroupCommentLiked |

**Note:** All notification creation logic is duplicated between services with only the enum changing.

---

## Appendix C: File Deletion Logic

### PostService - Complete Implementation

```csharp
if (post.Images.Any())
{
    var cacheBasePath = Path.Combine(_environment.ContentRootPath, "AppData", "cache", "resized-images");
    
    foreach (var postImage in post.Images)
    {
        // Parse URL: /api/secure-files/post-image/{userId}/{imageId}/{filename}
        var urlParts = postImage.ImageUrl.Split('/');
        if (urlParts.Length >= 3)
        {
            var imageUserId = urlParts[^3];
            var filename = urlParts[^1];

            // Delete original
            var originalImagePath = Path.Combine(
                _environment.ContentRootPath,
                "AppData", "uploads", "posts",
                imageUserId, "images", filename
            );
            File.Delete(originalImagePath);

            // Delete cached thumbnails
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var thumbnailPath = Path.Combine(cacheBasePath, $"{fileNameWithoutExtension}_thumbnail.jpg");
            var mediumPath = Path.Combine(cacheBasePath, $"{fileNameWithoutExtension}_medium.jpg");
            
            File.Delete(thumbnailPath);
            File.Delete(mediumPath);
        }
    }
    
    context.PostImages.RemoveRange(post.Images);
}
```

### GroupPostService - Missing Implementation

```csharp
// Delete the post (images will cascade)
context.GroupPosts.Remove(post);
```

⚠️ **CRITICAL:** Files are never deleted, leading to disk space leaks!

---

*End of Analysis Document*
