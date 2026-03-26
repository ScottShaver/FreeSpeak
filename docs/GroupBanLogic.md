# Group Ban Logic Documentation

## What Happens When a User is Banned?

When an administrator or moderator bans a user from a group, the following actions occur:

### 1. **User is Removed from Group Membership**
   - The user's `GroupUser` record is **deleted** from the database
   - This means they are no longer a member of the group
   - All membership data (role assignments, join date, post count, group points) is lost

### 2. **Ban Record is Created**
   - A new `GroupBannedMember` record is created with:
     - `GroupId`: The group they are banned from
     - `UserId`: The user who was banned
     - `BannedAt`: Timestamp of when the ban occurred

### 3. **Access Restrictions**
   - Banned users cannot:
     - View the group's home page
     - Access group content
     - Post or comment in the group
     - Send join requests to the group

### 4. **Audit Trail**
   - The ban action is logged in the audit log system with:
     - Who performed the ban
     - Which user was banned
     - When the ban occurred
     - The group information

## Unbanning a User

When a user is unbanned:

### 1. **Ban Record is Removed**
   - The `GroupBannedMember` record is deleted from the database

### 2. **User Can Rejoin**
   - The user can now send a new join request to the group
   - OR the user can be re-invited to the group
   - They start fresh with no previous membership data

### 3. **No Automatic Rejoining**
   - Unbanning does NOT automatically make them a member again
   - They must go through the join process again (request or invitation)
   - This is intentional to prevent accidental unbans from immediately granting access

## Permission Requirements

### Who Can Ban Users?
- **Group Creator**: Can ban anyone except themselves
- **Group Administrators**: Can ban regular members and moderators
- **Group Moderators**: Can ban regular members (NOT admins)
- **System Administrators**: Can ban anyone except the group creator

### Who Can Unban Users?
- **Group Administrators**: Yes
- **Group Moderators**: Yes
- **System Administrators**: Yes

### Special Rules
- The group creator **cannot be banned**
- Moderators **cannot ban administrators**
- A user already banned cannot be banned again

## Implementation Details

### Database Tables Affected

**GroupUsers** (Membership table)
- Record is **DELETED** when user is banned
- Fields: GroupId, UserId, IsAdmin, IsModerator, JoinedAt, PostCount, GroupPoints

**GroupBannedMembers** (Ban list table)
- Record is **CREATED** when user is banned
- Record is **DELETED** when user is unbanned
- Fields: Id, GroupId, UserId, BannedAt

### Service Methods

**GroupBannedMemberService.BanUserAsync()**
```csharp
public async Task<(bool Success, string? ErrorMessage)> BanUserAsync(
    int groupId, 
    string userId, 
    string bannedByUserId)
```

**GroupBannedMemberService.UnbanUserAsync()**
```csharp
public async Task<(bool Success, string? ErrorMessage)> UnbanUserAsync(
    int groupId, 
    string userId, 
    string unbannedByUserId)
```

**GroupBannedMemberService.IsUserBannedAsync()**
```csharp
public async Task<bool> IsUserBannedAsync(int groupId, string userId)
```

**GroupBannedMemberService.GetBannedMembersAsync()**
```csharp
public async Task<List<GroupBannedMember>> GetBannedMembersAsync(
    int groupId, 
    int skip = 0, 
    int take = 50)
```

## UI Features

### Banned Members Tab
- **Paginated List**: Shows 20 banned members at a time
- **Scrollable Container**: Max height of 500px (400px on mobile)
- **Search Functionality**: Filter banned members by name
- **Unban Action**: One-click unban with confirmation
- **Visual Indicators**: Red-themed UI to indicate ban status
- **Timestamps**: Shows when each user was banned
- **Load More**: Pagination button to load additional banned members

### Design Considerations
- Banned member avatars are shown with reduced opacity (0.7) to indicate inactive status
- Red color scheme throughout to visually distinguish from active members
- Expandable items to view unban options
- Permission checks disable unban button if user lacks permissions
- Success/error messages shown inline per member
