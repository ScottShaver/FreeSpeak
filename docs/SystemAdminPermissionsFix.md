# System Administrator Permissions Fix

## Issue
System Administrators were unable to ban or remove members from groups despite having full system permissions. The error messages displayed were:
- "You must be an admin or moderator to ban users."
- "Only group creator or admins can remove members."

## Root Cause
The service methods for banning, unbanning, and removing members were only checking for group-level permissions (group admin/moderator) and not checking if the user was a System Administrator.

## Changes Made

### 1. GroupBannedMemberService.cs - BanUserAsync Method
**Before:** Only checked if the user was a group admin or moderator.

**After:** Now checks if the user is a System Administrator first. If they are, they bypass the group-level permission checks.

```csharp
// Check if banner is a system administrator
var isSystemAdmin = await context.UserRoles
    .Join(context.Roles,
        ur => ur.RoleId,
        r => r.Id,
        (ur, r) => new { ur.UserId, r.Name })
    .AnyAsync(x => x.UserId == bannedByUserId && x.Name == "System Administrator");

// Now checks: !isSystemAdmin && (not group admin or moderator)
if (!isSystemAdmin && (bannerMembership == null || (!bannerMembership.IsAdmin && !bannerMembership.IsModerator)))
{
    return (false, "You must be an admin or moderator to ban users.");
}
```

**Additional Fix:** System Administrators can now ban group admins (previously only group admins could ban other admins).

### 2. GroupBannedMemberService.cs - UnbanUserAsync Method
**Before:** Only checked if the user was a group admin or moderator.

**After:** Now checks if the user is a System Administrator first, allowing them to unban users.

```csharp
// Check if unbanner is a system administrator
var isSystemAdmin = await context.UserRoles
    .Join(context.Roles,
        ur => ur.RoleId,
        r => r.Id,
        (ur, r) => new { ur.UserId, r.Name })
    .AnyAsync(x => x.UserId == unbannedByUserId && x.Name == "System Administrator");

if (!isSystemAdmin && (unbannerMembership == null || (!unbannerMembership.IsAdmin && !unbannerMembership.IsModerator)))
{
    return (false, "You must be an admin or moderator to unban users.");
}
```

### 3. GroupMemberService.cs - RemoveMemberAsync Method
**Before:** Only checked if the user was the group creator or a group admin.

**After:** Now checks if the user is a System Administrator first.

```csharp
// Check if requester is system administrator
var isSystemAdmin = await context.UserRoles
    .Join(context.Roles,
        ur => ur.RoleId,
        r => r.Id,
        (ur, r) => new { ur.UserId, r.Name })
    .AnyAsync(x => x.UserId == requesterId && x.Name == "System Administrator");

if (!isSystemAdmin && !isCreator && !isAdmin)
{
    return (false, "Only group creator or admins can remove members.");
}
```

## System Administrator Permissions Matrix

| Action | Group Creator | Group Admin | Group Moderator | System Admin |
|--------|--------------|-------------|-----------------|--------------|
| Ban regular member | ✅ | ✅ | ✅ | ✅ |
| Ban moderator | ✅ | ✅ | ❌ | ✅ |
| Ban admin | ✅ | ✅ | ❌ | ✅ |
| Ban creator | ❌ | ❌ | ❌ | ❌ |
| Unban any member | ✅ | ✅ | ✅ | ✅ |
| Remove regular member | ✅ | ✅ | ❌ | ✅ |
| Remove moderator | ✅ | ✅ | ❌ | ✅ |
| Remove admin | ✅ | ✅ | ❌ | ✅ |
| Remove creator | ❌ | ❌ | ❌ | ❌ |
| Assign admin role | ✅ | ❌ | ❌ | ✅ |
| Assign moderator role | ✅ | ✅ | ❌ | ✅ |

## Notes
- System Administrators have god-mode permissions across all groups
- The only restriction is that even System Administrators cannot ban or remove the group creator
- System Administrators do NOT need to be members of a group to perform these actions
- Role assignment methods (`SetAdminAsync` and `SetModeratorAsync`) were already properly checking for System Administrator status

## Testing
After these changes, System Administrators can now:
1. ✅ Ban any member (except the creator) from any group
2. ✅ Unban any member from any group
3. ✅ Remove any member (except the creator) from any group
4. ✅ Assign admin roles in any group
5. ✅ Assign moderator roles in any group
