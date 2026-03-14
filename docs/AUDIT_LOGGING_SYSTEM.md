# Audit Logging System Documentation

## Overview

The FreeSpeak audit logging system provides comprehensive tracking of user and administrative actions throughout the application. This system is essential for:

- **Security Compliance**: Track user authentication, data access, and sensitive operations
- **Administrative Oversight**: Monitor group moderation, user management, and content decisions
- **User Accountability**: Record user actions for dispute resolution and policy enforcement
- **System Analytics**: Analyze usage patterns and identify potential issues

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────────┐
│                        Audit Logging System                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐    ┌──────────────────┐    ┌─────────────┐ │
│  │  ActionCategory │───▶│ AuditLogDetails  │───▶│  AuditLog   │ │
│  │     (Enum)      │    │    (Classes)     │    │   (Entity)  │ │
│  └─────────────────┘    └──────────────────┘    └─────────────┘ │
│           │                      │                     │        │
│           ▼                      ▼                     ▼        │
│  ┌─────────────────┐    ┌──────────────────┐    ┌─────────────┐ │
│  │   Defines the   │    │ Strongly-typed   │    │  Database   │ │
│  │  type of action │    │ detail objects   │    │   storage   │ │
│  └─────────────────┘    └──────────────────┘    └─────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Key Files

| File | Purpose |
|------|---------|
| `Data/ActionCategory.cs` | Enumeration of all action types |
| `Data/AuditLog.cs` | Entity model for database storage |
| `Data/AuditLogDetails/*.cs` | Strongly-typed detail classes |
| `Repositories/AuditLogRepository.cs` | Data access layer |
| `Repositories/Abstractions/IAuditLogRepository.cs` | Repository interface |

---

## ActionCategory Enumeration

The `ActionCategory` enum defines all types of auditable actions in the system:

### User Actions (0-14)

| Value | Name | Description |
|-------|------|-------------|
| 0 | `UserProfileChange` | User profile updates (bio, display name, location) |
| 1 | `UserLogin` | User authentication success |
| 2 | `UserLogout` | User session termination |
| 3 | `UserGroupPost` | User creates a post in a group |
| 4 | `UserPost` | User creates a feed post |
| 5 | `UserPersonalData` | Personal data operations (export, deletion) |
| 6 | `UserPassword` | Password changes or resets |
| 7 | `UserPreference` | User preference/settings changes |
| 8 | `UserFriendsFind` | Friend search/discovery activities |
| 9 | `UserFriendsRequest` | Friend request operations |
| 10 | `UserGroupFind` | Group search/discovery activities |
| 11 | `UserGroupRequests` | Group join requests |
| 12 | `UserGroupLeave` | User leaves a group |
| 13 | `UserUpload` | File/media uploads |
| 14 | `UserNotification` | Notification interactions |

### Group Admin Actions (15-21)

| Value | Name | Description |
|-------|------|-------------|
| 15 | `GroupAdminCreateGroup` | Administrator creates a new group |
| 16 | `GroupAdminEditGroup` | Administrator modifies group settings |
| 17 | `GroupAdminBanUser` | Administrator bans a user from group |
| 18 | `GroupAdminCloseGroup` | Administrator closes/deletes a group |
| 19 | `GroupAdminApproveJoinRequest` | Administrator approves join request |
| 20 | `GroupAdminChangeMemberRole` | Administrator changes member role |
| 21 | `GroupAdminDenyJoinRequest` | Administrator denies join request |

### Group Moderator Actions (22-25)

| Value | Name | Description |
|-------|------|-------------|
| 22 | `GroupModeratorApprovePost` | Moderator approves pending post |
| 23 | `GroupModeratorDeclinePost` | Moderator declines pending post |
| 24 | `GroupModeratorRemovePost` | Moderator removes existing post |
| 25 | `GroupModeratorRemovePostComment` | Moderator removes a comment |

### System Moderator Actions (26-27)

| Value | Name | Description |
|-------|------|-------------|
| 26 | `SystemModeratorRemovePost` | System mod removes post (platform-wide) |
| 27 | `SystemModeratorRemovePostComment` | System mod removes comment (platform-wide) |

### User Group Participation (28-29)

| Value | Name | Description |
|-------|------|-------------|
| 28 | `UserGroupAgreeToRules` | User agrees to group rules |
| 29 | `UserGroupDeclineRules` | User declines group rules |

---

## AuditLogDetails Classes

Each action category has a corresponding detail class that captures action-specific information.

### Naming Convention

```
{ActionCategory}Details.cs
```

Example: `GroupAdminBanUserDetails.cs` for `ActionCategory.GroupAdminBanUser`

### Common Properties Pattern

Most detail classes follow these patterns:

```csharp
// For group-related actions
public int GroupId { get; set; }
public string GroupName { get; set; } = string.Empty;

// For user-targeted actions
public string TargetUserId { get; set; } = string.Empty;
public string? TargetUserDisplayName { get; set; }

// For content-related actions
public int PostId { get; set; }
public string? ContentSummary { get; set; }

// For actions with reasons
public string? Reason { get; set; }
```

### Example Detail Classes

#### GroupAdminBanUserDetails

```csharp
public class GroupAdminBanUserDetails
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string BannedUserId { get; set; } = string.Empty;
    public string? BannedUserDisplayName { get; set; }
    public string? Reason { get; set; }
    public bool IsPermanent { get; set; } = true;
    public int? BanDurationDays { get; set; }
}
```

#### GroupAdminChangeMemberRoleDetails

```csharp
public class GroupAdminChangeMemberRoleDetails
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public string? TargetUserDisplayName { get; set; }
    public string OldRole { get; set; } = string.Empty;  // "Member", "Moderator", "Admin"
    public string NewRole { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
```

---

## Usage Patterns

### Basic Audit Logging

```csharp
// Inject the repository
private readonly IAuditLogRepository _auditLogRepository;

// Log an action with strongly-typed details
await _auditLogRepository.LogActionAsync(
    userId,
    ActionCategory.GroupAdminBanUser,
    new GroupAdminBanUserDetails
    {
        GroupId = groupId,
        GroupName = group.Name,
        BannedUserId = targetUserId,
        BannedUserDisplayName = $"{user.FirstName} {user.LastName}",
        IsPermanent = true
    });
```

### Capturing Before/After State

For edit operations, capture the original values before making changes:

```csharp
public async Task<(bool Success, string? ErrorMessage)> UpdateGroupAsync(
    int groupId, string userId, string? name, bool? isPublic)
{
    var group = await context.Groups.FindAsync(groupId);

    // Capture original values BEFORE changes
    var changedFields = new List<string>();
    var oldName = group.Name;
    var oldIsPublic = group.IsPublic;

    // Make changes and track what changed
    if (!string.IsNullOrWhiteSpace(name) && group.Name != name)
    {
        changedFields.Add("Name");
        group.Name = name;
    }

    if (isPublic.HasValue && group.IsPublic != isPublic.Value)
    {
        changedFields.Add("IsPublic");
        group.IsPublic = isPublic.Value;
    }

    await context.SaveChangesAsync();

    // Only log if changes were made
    if (changedFields.Count > 0)
    {
        await _auditLogRepository.LogActionAsync(userId, ActionCategory.GroupAdminEditGroup, 
            new GroupAdminEditGroupDetails
            {
                GroupId = groupId,
                GroupName = group.Name,
                ChangedFields = changedFields,
                OldName = changedFields.Contains("Name") ? oldName : null,
                NewName = changedFields.Contains("Name") ? group.Name : null,
                OldIsPublic = changedFields.Contains("IsPublic") ? oldIsPublic : null,
                NewIsPublic = changedFields.Contains("IsPublic") ? group.IsPublic : null
            });
    }
}
```

### Role Change Tracking

```csharp
// Determine old role based on current state
var oldRole = groupUser.IsAdmin ? "Admin" : (groupUser.IsModerator ? "Moderator" : "Member");

// Make the change
groupUser.IsAdmin = isAdmin;
await context.SaveChangesAsync();

// Determine new role
var newRole = isAdmin ? "Admin" : (groupUser.IsModerator ? "Moderator" : "Member");

// Log the role change
await _auditLogRepository.LogActionAsync(requesterId, ActionCategory.GroupAdminChangeMemberRole,
    new GroupAdminChangeMemberRoleDetails
    {
        GroupId = groupId,
        GroupName = group.Name,
        TargetUserId = targetUserId,
        TargetUserDisplayName = $"{user.FirstName} {user.LastName}",
        OldRole = oldRole,
        NewRole = newRole
    });
```

---

## Adding New Audit Categories

### Step 1: Add to ActionCategory Enum

```csharp
// In Data/ActionCategory.cs
/// <summary>
/// Action when [describe the action].
/// [Additional context about what this tracks].
/// </summary>
NewActionCategory = 30,
```

### Step 2: Create Detail Class

```csharp
// In Data/AuditLogDetails/NewActionCategoryDetails.cs
namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for [action description] audit log entries.
    /// [Additional context about tracked data].
    /// </summary>
    public class NewActionCategoryDetails
    {
        /// <summary>
        /// Gets or sets [property description].
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        // Add relevant properties...
    }
}
```

### Step 3: Add Audit Logging Call

```csharp
// In the appropriate service method
await _auditLogRepository.LogActionAsync(
    userId,
    ActionCategory.NewActionCategory,
    new NewActionCategoryDetails
    {
        PropertyName = value
    });
```

### Step 4: Inject Repository (if needed)

```csharp
// Add to constructor
public MyService(
    // ... other dependencies
    IAuditLogRepository auditLogRepository)
{
    _auditLogRepository = auditLogRepository;
}
```

---

## Administration Screen Usage

### Querying Audit Logs

The `IAuditLogRepository` provides methods for retrieving audit data:

```csharp
// Get logs for a specific user
var userLogs = await _auditLogRepository.GetUserAuditLogsAsync(userId, pageNumber, pageSize);

// Get logs by action category (if implemented)
var moderationLogs = await _auditLogRepository.GetLogsByActionCategoryAsync(
    ActionCategory.GroupModeratorRemovePost, 
    startDate, 
    endDate);

// Get logs for a specific group (if implemented)
var groupLogs = await _auditLogRepository.GetGroupAuditLogsAsync(groupId, pageNumber, pageSize);
```

### Admin Dashboard Use Cases

#### 1. User Activity Timeline

Display a chronological view of user actions:

```razor
@foreach (var log in userAuditLogs)
{
    <div class="audit-entry">
        <span class="timestamp">@log.ActionStamp.ToLocalTime()</span>
        <span class="action">@GetActionDescription(log.ActionCategory)</span>
        <span class="details">@FormatDetails(log)</span>
    </div>
}
```

#### 2. Moderation Activity Report

Track moderator actions across groups:

```csharp
// Filter for moderation actions
var moderationCategories = new[]
{
    ActionCategory.GroupModeratorApprovePost,
    ActionCategory.GroupModeratorDeclinePost,
    ActionCategory.GroupModeratorRemovePost,
    ActionCategory.GroupModeratorRemovePostComment,
    ActionCategory.GroupAdminBanUser
};

var moderationLogs = allLogs
    .Where(l => moderationCategories.Contains(Enum.Parse<ActionCategory>(l.ActionCategory)))
    .OrderByDescending(l => l.ActionStamp);
```

#### 3. Security Audit

Monitor authentication and sensitive operations:

```csharp
var securityCategories = new[]
{
    ActionCategory.UserLogin,
    ActionCategory.UserLogout,
    ActionCategory.UserPassword,
    ActionCategory.UserPersonalData
};

var securityLogs = await GetLogsByCategories(securityCategories, last30Days);
```

#### 4. Group Administration History

Review all administrative actions for a group:

```csharp
// Parse and filter by GroupId in ActionDetails
var groupAdminLogs = allLogs
    .Where(l => l.ActionCategory.StartsWith("GroupAdmin") || 
                l.ActionCategory.StartsWith("GroupModerator"))
    .Where(l => {
        var details = JsonSerializer.Deserialize<JsonElement>(l.ActionDetails);
        return details.TryGetProperty("groupId", out var gid) && 
               gid.GetInt32() == targetGroupId;
    });
```

### Deserializing Action Details

Use the `AuditLogDetailsSerializer` to parse details:

```csharp
// Deserialize to specific type
var banDetails = AuditLogDetailsSerializer.FromJson<GroupAdminBanUserDetails>(log.ActionDetails);

if (banDetails != null)
{
    Console.WriteLine($"Banned user: {banDetails.BannedUserDisplayName}");
    Console.WriteLine($"From group: {banDetails.GroupName}");
    Console.WriteLine($"Reason: {banDetails.Reason ?? "Not specified"}");
}
```

### Display Formatting Example

```csharp
public string FormatAuditLogEntry(AuditLog log)
{
    var category = Enum.Parse<ActionCategory>(log.ActionCategory);

    return category switch
    {
        ActionCategory.GroupAdminBanUser => FormatBanAction(log),
        ActionCategory.GroupAdminChangeMemberRole => FormatRoleChange(log),
        ActionCategory.GroupModeratorRemovePost => FormatPostRemoval(log),
        _ => $"{category}: {log.ActionDetails}"
    };
}

private string FormatBanAction(AuditLog log)
{
    var details = AuditLogDetailsSerializer.FromJson<GroupAdminBanUserDetails>(log.ActionDetails);
    return $"Banned {details?.BannedUserDisplayName ?? "Unknown"} from {details?.GroupName ?? "Unknown Group"}";
}

private string FormatRoleChange(AuditLog log)
{
    var details = AuditLogDetailsSerializer.FromJson<GroupAdminChangeMemberRoleDetails>(log.ActionDetails);
    return $"Changed {details?.TargetUserDisplayName}'s role from {details?.OldRole} to {details?.NewRole}";
}
```

---

## Best Practices

### 1. Always Log After Success

Only create audit log entries after the operation succeeds:

```csharp
// ✅ Correct: Log after successful operation
await context.SaveChangesAsync();
await _auditLogRepository.LogActionAsync(...);

// ❌ Incorrect: Logging before operation completes
await _auditLogRepository.LogActionAsync(...);
await context.SaveChangesAsync(); // Could fail!
```

### 2. Don't Let Audit Logging Break Operations

The audit repository handles exceptions internally:

```csharp
// In AuditLogRepository.LogActionAsync
catch (Exception ex)
{
    _logger.LogError(ex, "Error logging action...");
    // Don't throw - audit logging should not break the main flow
}
```

### 3. Include Sufficient Context

Always include enough information to understand the action without querying other tables:

```csharp
// ✅ Good: Includes names for display
new GroupAdminBanUserDetails
{
    GroupId = groupId,
    GroupName = group.Name,  // Include name, not just ID
    BannedUserId = userId,
    BannedUserDisplayName = $"{user.FirstName} {user.LastName}"
}

// ❌ Less useful: Only IDs
new GroupAdminBanUserDetails
{
    GroupId = groupId,
    BannedUserId = userId
}
```

### 4. Truncate Large Content

For content summaries, truncate to reasonable lengths:

```csharp
ContentSummary = content?.Length > 100 
    ? content.Substring(0, 100) + "..." 
    : content
```

### 5. Use Consistent Terminology

Use standard terms for roles and actions:
- Roles: `"Member"`, `"Moderator"`, `"Admin"`
- Leave types: `"Voluntary"`, `"Removed"`, `"Banned"`, `"GroupDeleted"`
- Join types: `"DirectJoin"`, `"RequestSubmitted"`, `"RequestApproved"`, `"RequestDenied"`

---

## Database Considerations

### AuditLog Table Structure

```sql
CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    ActionStamp DATETIME2 NOT NULL,
    ActionCategory NVARCHAR(100) NOT NULL,
    ActionDetails NVARCHAR(MAX) NULL,
    -- Indexes for common queries
    INDEX IX_AuditLogs_UserId (UserId),
    INDEX IX_AuditLogs_ActionStamp (ActionStamp),
    INDEX IX_AuditLogs_ActionCategory (ActionCategory)
);
```

### Partitioning for Performance

For high-volume systems, consider partitioning by date:

```sql
-- Monthly partitions for efficient archival
CREATE PARTITION FUNCTION AuditLogDateRange (DATETIME2)
AS RANGE RIGHT FOR VALUES ('2025-01-01', '2025-02-01', ...);
```

### Retention Policy

Consider implementing a retention policy for audit logs:
- **Active Logs**: 90 days in primary table
- **Archive Logs**: 1-7 years in archive table or cold storage
- **Security-Sensitive Logs**: May require longer retention per compliance

---

## Testing

### Mock Repository for Unit Tests

```csharp
// In TestBase.cs
protected IAuditLogRepository CreateMockAuditLogRepository()
{
    var mock = new Mock<IAuditLogRepository>();
    mock.Setup(x => x.LogActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
        .Returns(Task.CompletedTask);
    mock.Setup(x => x.LogActionAsync(It.IsAny<string>(), It.IsAny<ActionCategory>(), It.IsAny<object>()))
        .Returns(Task.CompletedTask);
    return mock.Object;
}
```

### Verifying Audit Calls

```csharp
[Fact]
public async Task BanUser_ShouldCreateAuditLog()
{
    var auditMock = new Mock<IAuditLogRepository>();
    var service = new GroupBannedMemberService(dbFactory, logger, auditMock.Object);

    await service.BanUserAsync(groupId, userId, adminId);

    auditMock.Verify(x => x.LogActionAsync(
        adminId,
        ActionCategory.GroupAdminBanUser,
        It.Is<GroupAdminBanUserDetails>(d => 
            d.GroupId == groupId && 
            d.BannedUserId == userId)),
        Times.Once);
}
```

---

## Summary

The audit logging system provides a robust foundation for tracking all significant actions in the FreeSpeak application. By following the established patterns and best practices, developers can easily extend the system to cover new features while maintaining consistency and reliability.

Key takeaways:
1. Use strongly-typed detail classes for type safety
2. Log after successful operations
3. Include sufficient context for standalone understanding
4. Follow naming conventions for consistency
5. Leverage the data in admin screens for oversight and analytics
