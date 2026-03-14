# Audit Logging Quick Reference

> **Quick guide for developers implementing audit logging in FreeSpeak**

---

## 🚀 Quick Start

### 1. Inject the Repository

```csharp
private readonly IAuditLogRepository _auditLogRepository;

public MyService(..., IAuditLogRepository auditLogRepository)
{
    _auditLogRepository = auditLogRepository;
}
```

### 2. Log an Action

```csharp
await _auditLogRepository.LogActionAsync(
    userId,                              // Who performed the action
    ActionCategory.GroupAdminBanUser,    // What type of action
    new GroupAdminBanUserDetails         // Action-specific details
    {
        GroupId = groupId,
        GroupName = group.Name,
        BannedUserId = targetUserId,
        BannedUserDisplayName = $"{user.FirstName} {user.LastName}"
    });
```

---

## 📋 Action Categories at a Glance

| Category | Enum Value | Detail Class |
|----------|------------|--------------|
| **User Actions** |||
| Profile changes | `UserProfileChange` | `UserProfileChangeDetails` |
| Login | `UserLogin` | `UserLoginDetails` |
| Logout | `UserLogout` | `UserLogoutDetails` |
| Group post | `UserGroupPost` | `UserGroupPostDetails` |
| Feed post | `UserPost` | `UserPostDetails` |
| Personal data | `UserPersonalData` | `UserPersonalDataDetails` |
| Password | `UserPassword` | `UserPasswordDetails` |
| Preferences | `UserPreference` | `UserPreferenceDetails` |
| Find friends | `UserFriendsFind` | `UserFriendsFindDetails` |
| Friend requests | `UserFriendsRequest` | `UserFriendsRequestDetails` |
| Find groups | `UserGroupFind` | `UserGroupFindDetails` |
| Join requests | `UserGroupRequests` | `UserGroupRequestsDetails` |
| Leave group | `UserGroupLeave` | `UserGroupLeaveDetails` |
| Upload | `UserUpload` | `UserUploadDetails` |
| Notifications | `UserNotification` | `UserNotificationDetails` |
| Agree to rules | `UserGroupAgreeToRules` | `UserGroupAgreeToRulesDetails` |
| Decline rules | `UserGroupDeclineRules` | `UserGroupDeclineRulesDetails` |
| **Group Admin** |||
| Create group | `GroupAdminCreateGroup` | `GroupAdminCreateGroupDetails` |
| Edit group | `GroupAdminEditGroup` | `GroupAdminEditGroupDetails` |
| Ban user | `GroupAdminBanUser` | `GroupAdminBanUserDetails` |
| Close group | `GroupAdminCloseGroup` | `GroupAdminCloseGroupDetails` |
| Approve join | `GroupAdminApproveJoinRequest` | `GroupAdminApproveJoinRequestDetails` |
| Change role | `GroupAdminChangeMemberRole` | `GroupAdminChangeMemberRoleDetails` |
| Deny join | `GroupAdminDenyJoinRequest` | `GroupAdminDenyJoinRequestDetails` |
| **Group Moderator** |||
| Approve post | `GroupModeratorApprovePost` | `GroupModeratorApprovePostDetails` |
| Decline post | `GroupModeratorDeclinePost` | `GroupModeratorDeclinePostDetails` |
| Remove post | `GroupModeratorRemovePost` | `GroupModeratorRemovePostDetails` |
| Remove comment | `GroupModeratorRemovePostComment` | `GroupModeratorRemovePostCommentDetails` |
| **System Moderator** |||
| Remove post | `SystemModeratorRemovePost` | `SystemModeratorRemovePostDetails` |
| Remove comment | `SystemModeratorRemovePostComment` | `SystemModeratorRemovePostCommentDetails` |

---

## 📝 Common Code Patterns

### Pattern 1: Simple Action Logging

```csharp
// Log after successful operation
await context.SaveChangesAsync();

await _auditLogRepository.LogActionAsync(userId, ActionCategory.GroupAdminCreateGroup,
    new GroupAdminCreateGroupDetails
    {
        GroupId = group.Id,
        GroupName = group.Name,
        IsPublic = group.IsPublic
    });
```

### Pattern 2: Edit with Change Tracking

```csharp
// 1. Capture BEFORE state
var oldName = group.Name;
var changedFields = new List<string>();

// 2. Make changes and track
if (newName != group.Name)
{
    changedFields.Add("Name");
    group.Name = newName;
}

await context.SaveChangesAsync();

// 3. Log only if changed
if (changedFields.Count > 0)
{
    await _auditLogRepository.LogActionAsync(userId, ActionCategory.GroupAdminEditGroup,
        new GroupAdminEditGroupDetails
        {
            GroupId = groupId,
            GroupName = group.Name,
            ChangedFields = changedFields,
            OldName = oldName,
            NewName = group.Name
        });
}
```

### Pattern 3: Role Change Tracking

```csharp
// Determine old role
var oldRole = user.IsAdmin ? "Admin" : (user.IsModerator ? "Moderator" : "Member");

// Make change
user.IsAdmin = true;
await context.SaveChangesAsync();

// Log with before/after
await _auditLogRepository.LogActionAsync(requesterId, ActionCategory.GroupAdminChangeMemberRole,
    new GroupAdminChangeMemberRoleDetails
    {
        GroupId = groupId,
        GroupName = group.Name,
        TargetUserId = userId,
        OldRole = oldRole,
        NewRole = "Admin"
    });
```

### Pattern 4: Content Truncation

```csharp
ContentSummary = content?.Length > 100 
    ? content.Substring(0, 100) + "..." 
    : content
```

---

## ✅ Do's and Don'ts

| ✅ Do | ❌ Don't |
|-------|---------|
| Log **after** `SaveChangesAsync()` | Log before operation completes |
| Include display names, not just IDs | Store only foreign keys |
| Truncate long content (100-200 chars) | Store full post content |
| Use standard role names | Invent new terminology |
| Follow naming conventions | Create ad-hoc detail classes |

### Standard Terminology

| Type | Values |
|------|--------|
| **Roles** | `"Member"`, `"Moderator"`, `"Admin"` |
| **Leave Types** | `"Voluntary"`, `"Removed"`, `"Banned"`, `"GroupDeleted"` |
| **Join Types** | `"DirectJoin"`, `"RequestSubmitted"`, `"RequestApproved"`, `"RequestDenied"` |
| **Post Types** | `"FeedPost"`, `"GroupPost"` |

---

## 🆕 Adding a New Category

### Step 1: Add enum value
```csharp
// Data/ActionCategory.cs
/// <summary>
/// [Description of the action]
/// </summary>
MyNewAction = 30,
```

### Step 2: Create detail class
```csharp
// Data/AuditLogDetails/MyNewActionDetails.cs
namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for [action] audit log entries.
    /// </summary>
    public class MyNewActionDetails
    {
        public int EntityId { get; set; }
        public string? Description { get; set; }
    }
}
```

### Step 3: Add logging call
```csharp
await _auditLogRepository.LogActionAsync(userId, ActionCategory.MyNewAction,
    new MyNewActionDetails { EntityId = id, Description = "..." });
```

---

## 🔍 Reading Audit Logs

### Get User's Logs
```csharp
var logs = await _auditLogRepository.GetUserAuditLogsAsync(userId, page, pageSize);
```

### Deserialize Details
```csharp
var details = AuditLogDetailsSerializer.FromJson<GroupAdminBanUserDetails>(log.ActionDetails);
Console.WriteLine($"Banned: {details?.BannedUserDisplayName}");
```

### Format for Display
```csharp
var formatted = AuditLogDetailsSerializer.ToFormattedJson(details); // Pretty JSON
```

---

## 🧪 Testing

### Mock in Unit Tests
```csharp
var auditRepo = CreateMockAuditLogRepository(); // From TestBase
var service = new MyService(dbFactory, logger, auditRepo);
```

### Verify Audit Was Called
```csharp
auditMock.Verify(x => x.LogActionAsync(
    userId,
    ActionCategory.GroupAdminBanUser,
    It.Is<GroupAdminBanUserDetails>(d => d.BannedUserId == targetId)),
    Times.Once);
```

---

## 📁 File Locations

```
FreeSpeakWeb/
├── Data/
│   ├── ActionCategory.cs              # Enum definitions
│   ├── AuditLog.cs                     # Entity model
│   └── AuditLogDetails/
│       ├── AuditLogDetailsSerializer.cs
│       ├── GroupAdminBanUserDetails.cs
│       ├── GroupAdminChangeMemberRoleDetails.cs
│       └── ... (other detail classes)
├── Repositories/
│   ├── Abstractions/
│   │   └── IAuditLogRepository.cs
│   └── AuditLogRepository.cs
└── Services/
    └── (services that use audit logging)
```

---

## 📖 Full Documentation

See [AUDIT_LOGGING_SYSTEM.md](./AUDIT_LOGGING_SYSTEM.md) for complete documentation.
