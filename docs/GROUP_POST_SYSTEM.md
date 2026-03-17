# Group Post System Documentation

## Overview
The Group Post System provides a complete posting and interaction system for groups, parallel to the main feed post system. It includes posts, comments, likes, and moderation features specifically designed for group contexts.

## Database Tables

### Core Tables

#### GroupPosts
Stores posts made within groups.

**Columns:**
- `Id` (int, PK) - Unique identifier
- `GroupId` (int, FK → Groups) - The group this post belongs to
- `AuthorId` (string, FK → Users) - User who created the post
- `Content` (string) - Post text content
- `CreatedAt` (DateTime) - When the post was created
- `UpdatedAt` (DateTime?) - When the post was last updated (null if never updated)
- `LikeCount` (int) - Cached count of likes
- `CommentCount` (int) - Cached count of comments (includes nested replies)
- `ShareCount` (int) - Cached count of shares
- `Status` (PostStatus enum) - Approval status: Pending (0), Posted (1), Declined (2)

**Indexes:**
- `GroupId`
- `AuthorId`
- `CreatedAt`
- Composite: `(GroupId, CreatedAt)` - For efficient retrieval of group posts

**Navigation Properties:**
- `Group` → Group
- `Author` → ApplicationUser
- `Comments` → Collection<GroupPostComment>
- `Likes` → Collection<GroupPostLike>
- `Images` → Collection<GroupPostImage>

#### GroupPostImages
Stores images attached to group posts.

**Columns:**
- `Id` (int, PK)
- `PostId` (int, FK → GroupPosts)
- `ImageUrl` (string) - URL/path to the image
- `DisplayOrder` (int) - Order for displaying multiple images (0-based)
- `UploadedAt` (DateTime)

**Indexes:**
- `PostId`
- Composite: `(PostId, DisplayOrder)`

#### GroupPostComments
Stores comments on group posts with support for nested replies.

**Columns:**
- `Id` (int, PK)
- `PostId` (int, FK → GroupPosts)
- `AuthorId` (string, FK → Users)
- `Content` (string) - Comment text
- `ImageUrl` (string?) - Optional image attached to comment
- `CreatedAt` (DateTime)
- `ParentCommentId` (int?) - For nested replies (null for top-level comments)

**Indexes:**
- `PostId`
- `AuthorId`
- `ParentCommentId`
- `CreatedAt`

**Navigation Properties:**
- `Post` → GroupPost
- `Author` → ApplicationUser
- `ParentComment` → GroupPostComment (for nested replies)
- `Replies` → Collection<GroupPostComment>

#### GroupPostLikes
Stores likes/reactions on group posts.

**Columns:**
- `Id` (int, PK)
- `PostId` (int, FK → GroupPosts)
- `UserId` (string, FK → Users)
- `Type` (LikeType enum) - Reaction type (Like, Love, Care, etc.)
- `CreatedAt` (DateTime)

**Indexes:**
- `PostId`
- `UserId`
- Composite Unique: `(PostId, UserId)` - User can only like a post once

#### GroupPostCommentLikes
Stores likes/reactions on group post comments.

**Columns:**
- `Id` (int, PK)
- `CommentId` (int, FK → GroupPostComments)
- `UserId` (string, FK → Users)
- `Type` (LikeType enum)
- `CreatedAt` (DateTime)

**Indexes:**
- `CommentId`
- `UserId`
- Composite Unique: `(CommentId, UserId)` - User can only like a comment once

### Support Tables

#### PinnedGroupPosts
Tracks posts that users have pinned for easy access.

**Columns:**
- `Id` (int, PK)
- `UserId` (string, FK → Users) - User who pinned the post
- `PostId` (int, FK → GroupPosts)
- `PinnedAt` (DateTime)

**Indexes:**
- `UserId`
- `PostId`
- `PinnedAt`
- Composite Unique: `(UserId, PostId)` - User can only pin a post once

#### GroupPostNotificationMutes
Tracks users who have muted notifications for specific group posts.

**Columns:**
- `Id` (int, PK)
- `PostId` (int, FK → GroupPosts)
- `UserId` (string, FK → Users)
- `MutedAt` (DateTime)

**Indexes:**
- `PostId`
- `UserId`
- Composite Unique: `(PostId, UserId)` - User can only mute a post once

#### GroupBannedMembers
Tracks users banned from groups.

**Columns:**
- `Id` (int, PK)
- `GroupId` (int, FK → Groups)
- `UserId` (string, FK → Users)
- `BannedAt` (DateTime)

**Indexes:**
- `GroupId`
- `UserId`
- `BannedAt`
- Composite Unique: `(GroupId, UserId)` - User can only be banned once per group

## Services

### GroupPostService
Comprehensive service for managing group posts and interactions.

**Post Operations:**
- `CreateGroupPostAsync` - Create new group post (status set based on group's RequiresPostApproval)
- `UpdateGroupPostAsync` - Update post content/images (resets status to Pending for declined posts)
- `DeleteGroupPostAsync` - Delete post (author, admin, or moderator)
- `GetGroupPostByIdAsync` - Retrieve specific post
- `GetGroupPostsAsync` - Get paginated posts for a group (only Posted status)
- `GetUserGroupPostsAsync` - Get posts by specific user in group
- `GetGroupPostCountAsync` - Get total post count

**Post Approval Operations:**
- `ApprovePostAsync` - Approve pending post (moderator/admin only)
- `DeclinePostAsync` - Decline pending post (moderator/admin only)
- `GetPendingPostsAsync` - Get posts awaiting approval
- `GetPendingPostCountAsync` - Get count of pending posts
- `GetUserPostsIncludingAllStatusesAsync` - Get user's own posts with any status

**Comment Operations:**
- `AddCommentAsync` - Add comment or reply
- `DeleteCommentAsync` - Delete comment (author, admin, or moderator)
- `GetCommentsAsync` - Get comments with nested replies

**Like Operations:**
- `LikePostAsync` - Like/react to post
- `UnlikePostAsync` - Remove like
- `GetUserLikeAsync` - Check user's like status

**Comment Like Operations:**
- `LikeCommentAsync` - Like/react to comment
- `UnlikeCommentAsync` - Remove comment like
- `GetUserCommentLikeAsync` - Check user's comment like status

**Notification Mute Operations:**
- `MutePostNotificationsAsync` - Mute notifications for post
- `UnmutePostNotificationsAsync` - Unmute notifications
- `IsPostNotificationMutedAsync` - Check mute status

### PinnedGroupPostService
Service for managing pinned group posts.

**Operations:**
- `PinGroupPostAsync` - Pin a post (requires group membership)
- `UnpinGroupPostAsync` - Unpin a post
- `IsGroupPostPinnedAsync` - Check if post is pinned
- `GetPinnedGroupPostsAsync` - Get all pinned posts for user
- `GetPinnedGroupPostsByGroupAsync` - Get pinned posts in specific group
- `GetPinnedGroupPostCountAsync` - Get count of pinned posts

### GroupBannedMemberService
Service for managing group bans.

**Operations:**
- `BanUserAsync` - Ban user from group (admin/moderator only)
- `UnbanUserAsync` - Remove ban (admin/moderator only)
- `IsUserBannedAsync` - Check if user is banned
- `GetBannedMembersAsync` - Get paginated list of banned members
- `GetBannedMemberCountAsync` - Get count of banned members
- `GetUserBansAsync` - Get all groups user is banned from

## Business Rules

### Post Approval System

Groups can enable post approval via the `RequiresPostApproval` setting:

#### When Enabled:
- New posts are created with `Status = Pending`
- Pending posts are only visible to the post author
- Moderators/admins see pending posts in the Moderation modal
- Moderators can **Approve** (sets `Status = Posted`) or **Decline** (sets `Status = Declined`)
- Approved posts become visible to all group members
- Declined posts can be edited by the author, which resets status to Pending

#### When Disabled:
- Posts are created with `Status = Posted` (immediately visible)
- All standard posting rules apply

#### Status Transitions:
```
Creating Post:
  RequiresPostApproval=false → Posted
  RequiresPostApproval=true  → Pending

Moderator Actions:
  Pending → Posted (Approve)
  Pending → Declined (Decline)

Author Edits:
  Pending  → Pending (no change)
  Declined → Pending (resubmitted for review)
  Posted   → Posted (no change)
```

#### Group Setting Changes:
When `RequiresPostApproval` is changed from false to true:
- All existing posts (except Declined) are automatically set to `Posted` status
- This ensures existing content remains visible to members

### Access Control

#### Posting
- Users must be group members to create posts
- Banned users cannot create posts
- If group requires approval, posts start as Pending
- Posts can be deleted by:
  - Post author
  - Group admins
  - Group moderators

#### Post Approval
- Only admins and moderators can approve/decline posts
- Approval sets post status to Posted (visible to all)
- Decline sets post status to Declined (only visible to author)
- Authors can edit declined posts to resubmit for approval

#### Commenting
- Users must be group members to comment
- Banned users cannot comment
- Comments can be deleted by:
  - Comment author
  - Group admins
  - Group moderators

#### Liking
- Users must be group members to like posts/comments
- Banned users cannot like posts/comments
- Users can change reaction types

#### Banning
- Only admins and moderators can ban users
- Group creator cannot be banned
- Moderators cannot ban admins
- Banning removes user from group membership
- Already banned users cannot be banned again

### Data Integrity

#### Cascade Deletes
When a **GroupPost** is deleted:
- All associated `GroupPostImages` are deleted
- All associated `GroupPostComments` are deleted
- All associated `GroupPostLikes` are deleted
- All associated `PinnedGroupPosts` are deleted
- All associated `GroupPostNotificationMutes` are deleted

When a **GroupPostComment** is deleted:
- All child replies (`ParentCommentId` references) are deleted
- All associated `GroupPostCommentLikes` are deleted
- Post comment count is decremented

When a **User** is banned:
- Group membership is removed
- User can no longer interact with group

#### Cached Counts
- `LikeCount` updated when likes are added/removed
- `CommentCount` updated when comments are added/removed (includes nested replies)
- Counts maintained for performance

## Security Considerations

1. **Group Membership Validation**: All operations verify group membership before allowing actions
2. **Ban Status Checks**: Operations check if user is banned before allowing actions
3. **Permission Hierarchies**: 
   - Admins can perform all moderation actions
   - Moderators can moderate content but cannot ban admins
   - Regular members can only modify their own content
4. **Unique Constraints**: Prevent duplicate likes, pins, and bans

## Performance Optimizations

1. **Indexed Queries**: Strategic indexes on foreign keys and composite columns
2. **Cached Counts**: Like and comment counts stored to avoid expensive COUNT queries
3. **Composite Indexes**: Optimized for common query patterns (e.g., group + date)
4. **Eager Loading**: Navigation properties loaded efficiently with `.Include()`

## Migration History

- `AddGroupPostTables` - Initial creation of group post tables
- `AddGroupPostCommentAndLikeTables` - Added comment and like tables
- `AddGroupPostNotificationMutes` - Added notification mute functionality
- `AddPostApprovalSystem` - Added PostStatus column and RequiresPostApproval to Groups

## Testing

Comprehensive unit tests with 100% pass rate:
- **GroupPostServiceTests**: 20 tests covering all operations
- **PinnedGroupPostServiceTests**: 10 tests for pinning functionality
- **GroupBannedMemberServiceTests**: 13 tests for ban management

See test files in `FreeSpeakWeb.Tests\Services\` for detailed test coverage.

## Future Enhancements

Potential areas for expansion:
- Group post analytics (views, engagement metrics)
- Scheduled group posts
- Group post templates
- Rich media embeds (polls, events)
- ~~Post approval workflows for moderated groups~~ ✅ Implemented
- Group post search and filtering
