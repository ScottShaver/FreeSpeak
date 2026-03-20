# Notification System Documentation

## Overview

The FreeSpeak notification system provides real-time updates to users about social interactions on the platform. Notifications are triggered automatically when other users interact with your content.

## Architecture

### Components

1. **Database Layer** (`Data/UserNotification.cs`, `Data/NotificationType.cs`)
   - `UserNotification` entity stores notification data
   - Indexed for performance (UserId, CreatedAt, IsRead, Type)
   - JSON data field for extensible metadata

2. **Service Layer** (`Services/NotificationService.cs`)
   - CRUD operations for notifications
   - Pagination support
   - Bulk operations (mark all read, delete read)
   - Unread count tracking

3. **Integration Layer** (`Services/PostService.cs`)
   - Automatic notification creation on social interactions
   - Integrated with post reactions, comments, and replies

4. **UI Layer** (`Components/Pages/Notifications.razor`, `Components/Shared/NotificationComponent.razor`)
   - Notification center page
   - Individual notification display
   - Navigation integration

## Notification Types

### PostLiked
**Triggered when**: Someone reacts to your post  
**Data stored**: `PostId`, `ReactorId`, `ReactorName`, `ReactorProfilePicture`, `ReactionType`  
**Badge display**: Shows actual reaction emoji (👍 ❤️ 😂 😮 😢 😠)  
**Example**: "john_doe reacted to your post with love"

### PostComment
**Triggered when**: Someone comments on your post  
**Data stored**: `PostId`, `CommentId`, `CommenterId`, `CommenterName`, `CommenterProfilePicture`  
**Badge display**: 💬 Chat bubble icon  
**Example**: "jane_smith commented on your post"

### CommentReply
**Triggered when**: Someone replies to your comment  
**Data stored**: `PostId`, `CommentId`, `CommenterId`, `CommenterName`, `CommenterProfilePicture`  
**Badge display**: ↩️ Reply icon  
**Example**: "bob_jones replied to your comment"

### CommentLiked
**Triggered when**: Someone reacts to your comment  
**Data stored**: `PostId`, `CommentId`, `ReactorId`, `ReactorName`, `ReactorProfilePicture`, `ReactionType`  
**Badge display**: Shows actual reaction emoji  
**Example**: "alice_wonder reacted to your comment with haha"

### FriendRequest
**Triggered when**: Someone sends you a friend request  
**Badge display**: ➕👤 Person plus icon  

### FriendAccepted
**Triggered when**: Someone accepts your friend request  
**Badge display**: ✓👤 Person check icon  

### Mention
**Triggered when**: Someone mentions you (future feature)  
**Badge display**: @ At symbol  

### System
**Triggered when**: System-generated notifications  
**Badge display**: ℹ️ Info circle icon  

## Smart Notification Logic

### When Notifications Are NOT Created:
- ❌ When you interact with your own content (posts/comments)
- ❌ When changing an existing reaction type (updates don't create new notifications)
- ❌ When removing a reaction

### When Notifications ARE Created:
- ✅ New reaction on someone else's post/comment
- ✅ New comment on someone else's post
- ✅ New reply to someone else's comment
- ✅ Friend request sent/accepted

## Database Schema

```sql
CREATE TABLE "UserNotifications" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Type" INTEGER NOT NULL,
    "Message" TEXT NOT NULL,
    "Data" TEXT NULL,
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL,
    "ExpiresAt" TIMESTAMP NULL,
    
    CONSTRAINT "FK_UserNotifications_AspNetUsers_UserId" 
        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_UserNotifications_UserId" ON "UserNotifications" ("UserId");
CREATE INDEX "IX_UserNotifications_CreatedAt" ON "UserNotifications" ("CreatedAt");
CREATE INDEX "IX_UserNotifications_IsRead" ON "UserNotifications" ("IsRead");
CREATE INDEX "IX_UserNotifications_Type" ON "UserNotifications" ("Type");
CREATE INDEX "IX_UserNotifications_ExpiresAt" ON "UserNotifications" ("ExpiresAt");
CREATE INDEX "IX_UserNotifications_UserId_IsRead_CreatedAt" 
    ON "UserNotifications" ("UserId", "IsRead", "CreatedAt" DESC);
```

### JSON Data Structure

The `Data` field stores JSON with notification-specific information:

**For Post Reactions:**
```json
{
    "PostId": 123,
    "ReactorId": "user-guid",
    "ReactorName": "john_doe",
    "ReactorProfilePicture": "/api/secure-files/profile-picture/...",
    "ReactionType": "Love"
}
```

**For Comments:**
```json
{
    "PostId": 123,
    "CommentId": 456,
    "CommenterId": "user-guid",
    "CommenterName": "jane_smith",
    "CommenterProfilePicture": "/api/secure-files/profile-picture/..."
}
```

## API Usage

### Creating Notifications

```csharp
// In PostService.cs - when a user reacts to a post
await _notificationService.CreateNotificationAsync(
    post.AuthorId,                    // Who receives the notification
    NotificationType.PostLiked,       // Type of notification
    $"{reactor.UserName} reacted to your post with {reactionText}",
    new { 
        PostId = postId, 
        ReactorId = userId, 
        ReactorName = reactor.UserName,
        ReactorProfilePicture = reactor.ProfilePictureUrl,
        ReactionType = reactionType.ToString() 
    }
);
```

### Retrieving Notifications

```csharp
// Get paginated notifications for a user
var notifications = await NotificationService.GetUserNotificationsAsync(
    userId: "user-guid",
    pageSize: 20,
    pageNumber: 1,
    isRead: null  // null = all, true = read only, false = unread only
);

// Get unread count
var unreadCount = await NotificationService.GetUnreadCountAsync(userId);
```

### Marking as Read

```csharp
// Mark single notification as read
await NotificationService.MarkAsReadAsync(notificationId, userId);

// Mark all as read
var (success, errorMessage, updatedCount) = 
    await NotificationService.MarkAllAsReadAsync(userId);
```

### Bulk Operations

```csharp
// Delete all read notifications
var (success, errorMessage, deletedCount) = 
    await NotificationService.DeleteReadNotificationsAsync(userId);

// Delete expired notifications (cleanup task)
await NotificationService.DeleteExpiredNotificationsAsync();
```

## UI Features

### Notification Center

**Location**: `/notifications` route  
**Access**: Authenticated users only  
**Features**:
- Tab navigation (All / Unread)
- Unread count badges
- Mark all as read button
- Clear read notifications button
- Infinite scroll with "Load more"
- Relative time display
- User avatars with reaction badges

### Navigation Integration

**Top Navigation Bar**:
- Bell icon (🔔) between "My Uploads" and profile icon
- Only visible when logged in
- Active state when on notifications page

**Left Sidebar Menu**:
- "Notifications" link with bell icon
- Only visible when logged in
- Active state highlighting

### Notification Click Behavior

When a notification is clicked:

1. **Mark as Read**: If unread, marks the notification as read
2. **Parse Data**: Extracts PostId and CommentId from JSON data
3. **Load Post**: Fetches full post with reactions and comments
4. **Open Modal**: Displays PostDetailModal with the post
5. **Scroll to Comment**: If CommentId exists, scrolls to and highlights that comment
6. **Animation**: 2-second blue flash animation on target comment

## Performance Considerations

### Database Indexes

Multiple indexes ensure fast queries:
- `UserId` - Fast user notification lookups
- `CreatedAt` - Chronological ordering
- `IsRead` - Filter by read status
- `Type` - Filter by notification type
- Composite `(UserId, IsRead, CreatedAt)` - Optimized for common queries

### Pagination

- Default page size: 20 notifications
- Prevents loading excessive data
- "Load more" button for additional pages

### Cleanup Strategy

Optional expiration system:
- Set `ExpiresAt` when creating notifications
- Background job can call `DeleteExpiredNotificationsAsync()`
- Keeps database size manageable

## Testing

### Unit Tests

Test notification creation in `PostServiceTests.cs`:
```csharp
[Fact]
public async Task AddOrUpdateReactionAsync_NewReaction_CreatesNotification()
{
    // Arrange
    var post = CreateTestPost(authorId: "user1");
    var reactor = CreateTestUser(id: "user2");
    
    // Act
    await PostService.AddOrUpdateReactionAsync(
        post.Id, reactor.Id, LikeType.Love);
    
    // Assert
    var notifications = await NotificationService
        .GetUserNotificationsAsync("user1", 10, 1);
    
    notifications.Should().HaveCount(1);
    notifications[0].Type.Should().Be(NotificationType.PostLiked);
    notifications[0].Message.Should()
        .Contain(reactor.UserName)
        .And.Contain("love");
}
```

### Integration Tests

Test end-to-end notification flow in `NotificationIntegrationTests.cs`:
```csharp
[Fact]
public async Task UserReactsToPost_NotificationAppears_ClickOpensPost()
{
    // 1. User B reacts to User A's post
    // 2. Verify notification created for User A
    // 3. Verify notification data contains PostId
    // 4. Verify unread count increased
    // 5. Click notification
    // 6. Verify post modal opens
    // 7. Verify notification marked as read
}
```

## Future Enhancements

### Planned Features
- [ ] Real-time push notifications (SignalR)
- [ ] Email digest for unread notifications
- [ ] Notification preferences/settings
- [ ] Mention system (@username)
- [ ] Group notifications (consolidate similar notifications)
- [ ] Sound/visual notifications in browser
- [ ] Notification history archive

### Optimization Opportunities
- [ ] Background job for expired notification cleanup
- [ ] Notification batching (combine similar notifications)
- [ ] Redis cache for unread counts
- [ ] WebSocket for real-time updates
- [ ] Notification templates system

## Troubleshooting

### Notifications not appearing
1. Check database migration is applied: `dotnet ef database update`
2. Verify NotificationService is registered in `Program.cs`
3. Check PostService has NotificationService injected
4. Verify user is authenticated

### Notification icons not showing
1. Check CSS file includes icon SVG definitions
2. Verify Bootstrap Icons classes are correctly named
3. Check browser console for CSS loading errors

### Modal not opening from notification
1. Verify notification Data field contains PostId
2. Check PostService.GetPostByIdAsync returns post
3. Verify PostDetailModal component is included in page
4. Check browser console for JavaScript errors

## Migration Guide

### Adding a New Notification Type

1. **Add enum value** to `NotificationType.cs`:
```csharp
public enum NotificationType
{
    // ... existing types
    NewFeature = 8
}
```

2. **Create notification** in appropriate service:
```csharp
await _notificationService.CreateNotificationAsync(
    recipientUserId,
    NotificationType.NewFeature,
    "Notification message here",
    new { /* custom data */ }
);
```

3. **Add UI support** in `NotificationComponent.razor`:
```csharp
case NotificationType.NewFeature:
    <span class="bi bi-custom-icon"></span>
    break;
```

4. **Add CSS icon** in `NotificationComponent.razor.css`:
```css
.bi-custom-icon {
    background-image: url("data:image/svg+xml,...");
}
```

## Best Practices

1. **Always include user context**: Store username and profile picture in notification data
2. **Keep messages concise**: Notification messages should be under 100 characters
3. **Use expiration for temporary notifications**: Set `ExpiresAt` for time-sensitive notifications
4. **Don't notify for own actions**: Always check if action user != content owner
5. **Batch similar notifications**: Consider grouping when multiple users perform same action
6. **Test notification creation**: Include notification tests when adding new features
7. **Monitor database size**: Implement cleanup strategy for old notifications

---

**Last Updated**: January 2025  
**Version**: 1.0
