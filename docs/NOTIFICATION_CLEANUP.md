# Notification Cleanup System

## Overview
The notification cleanup system automatically removes expired notifications using a background service with intelligent throttling to balance database cleanliness with performance.

## Architecture

### Components

1. **NotificationCleanupService** - Background service (IHostedService)
   - Runs every 5 minutes automatically
   - Cleans up expired notifications for all users
   - Throttled to prevent excessive database calls

2. **Throttling Mechanism**
   - Minimum interval between cleanups: 1 minute
   - Uses SemaphoreSlim for thread-safe locking
   - Prevents concurrent cleanup operations
   - Tracks last cleanup time globally

3. **NotificationService**
   - Provides `DeleteExpiredNotificationsAsync()` method
   - No longer called from retrieval methods
   - Can be manually triggered if needed

## How It Works

### Background Job Schedule

```
Application Start
      ↓
  Wait 1 minute (warm-up)
      ↓
  Run Cleanup ←────┐
      ↓            │
  Wait 5 minutes ──┘
```

### Throttling Logic

```csharp
// Cleanup will only run if:
1. At least 1 minute has passed since last cleanup
2. No other cleanup is currently running
3. Background job timer has triggered (every 5 minutes)
```

### Decision Flow

```
Cleanup Triggered
      ↓
Time since last cleanup >= 1 minute?
      ↓ No → Skip
      ↓ Yes
Can acquire lock?
      ↓ No → Skip
      ↓ Yes
Run cleanup
      ↓
Release lock
Update last cleanup time
```

## Configuration

### Intervals (in NotificationCleanupService.cs)

```csharp
// Minimum time between cleanup runs (throttle limit)
private static readonly TimeSpan MinimumCleanupInterval = TimeSpan.FromMinutes(1);

// How often the background job runs
private static readonly TimeSpan BackgroundJobInterval = TimeSpan.FromMinutes(5);
```

**To adjust:**
- **MinimumCleanupInterval**: Change to adjust throttling (e.g., 30 seconds, 2 minutes)
- **BackgroundJobInterval**: Change to adjust background job frequency (e.g., 10 minutes)

## Performance Benefits

### Before (Per-Call Cleanup)
```
User loads notifications → Query DB for expired → Delete expired → Query notifications
User checks badge count  → Query DB for expired → Delete expired → Count unread
User gets total count    → Query DB for expired → Delete expired → Count total

Result: 3+ cleanup operations per page load
```

### After (Background + Throttling)
```
Background job (every 5 minutes) → Delete expired
User loads notifications → Query notifications (fast)
User checks badge count  → Count unread (fast)
User gets total count    → Count total (fast)

Result: No cleanup overhead during user operations
```

### Impact
- **Reduced DB calls**: From 3+ per page load to 1 every 5 minutes
- **Faster queries**: No cleanup delay before retrieval
- **Scalability**: Cleanup cost doesn't grow with user activity
- **Throttling**: Prevents excessive cleanup if manually triggered

## Manual Triggering

While not normally needed, you can manually trigger cleanup:

```csharp
// From any service with IServiceProvider and ILogger
await NotificationCleanupService.TriggerCleanupAsync(serviceProvider, logger);

// This respects the 1-minute throttle limit
```

## Monitoring

### Check Last Cleanup Time

```csharp
var timeSinceLastCleanup = NotificationCleanupService.TimeSinceLastCleanup;
Console.WriteLine($"Last cleanup was {timeSinceLastCleanup.TotalMinutes} minutes ago");
```

### Check If Cleanup Should Run

```csharp
var shouldRun = NotificationCleanupService.ShouldRunCleanup;
Console.WriteLine($"Should run cleanup: {shouldRun}");
```

### Logs

The service logs cleanup operations:

**Debug Level:**
```
Skipping notification cleanup - last run was 45 seconds ago (minimum 60 seconds)
Notification cleanup completed - no expired notifications found
```

**Info Level:**
```
Notification Cleanup Service is starting
Notification cleanup completed - deleted 15 expired notifications
Notification Cleanup Service is stopping
```

**Warning Level:**
```
Notification cleanup failed: An error occurred while deleting expired notifications.
```

**Error Level:**
```
Error occurred during notification cleanup: [Exception details]
```

## Database Impact

### Query Executed
```sql
DELETE FROM "UserNotifications"
WHERE "ExpiresAt" IS NOT NULL 
  AND "ExpiresAt" < NOW()
```

### Indexed Query
The query uses the `ExpiresAt` index (defined in ApplicationDbContext):
```csharp
entity.HasIndex(n => n.ExpiresAt);
```

### Performance Characteristics
- **Frequency**: Every 5 minutes (throttled to max 1/minute)
- **Scope**: All users (system-wide)
- **Index**: Uses `ExpiresAt` index (fast)
- **Typical Count**: Varies based on notification retention settings
- **Transaction**: Single DELETE operation (fast)

## Thread Safety

The cleanup service is fully thread-safe:

1. **Static Lock**: `SemaphoreSlim` prevents concurrent cleanup
2. **Double-Check**: Verifies timing after acquiring lock
3. **Atomic Updates**: `_lastCleanupTime` updated after successful cleanup
4. **Single Instance**: Background service runs as singleton

## Graceful Shutdown

When the application stops:

1. Background service receives cancellation token
2. Current cleanup operation completes
3. Service logs shutdown message
4. No cleanup operations are interrupted

## Troubleshooting

### Cleanup Not Running

**Check logs for:**
```
Notification Cleanup Service is starting
```

**If not present:**
- Verify service is registered in Program.cs
- Check for startup errors

### Cleanup Running Too Often

**Check intervals:**
```csharp
MinimumCleanupInterval = TimeSpan.FromMinutes(1)  // Throttle
BackgroundJobInterval = TimeSpan.FromMinutes(5)   // Schedule
```

**Expected behavior:**
- Maximum 1 cleanup per minute (even if manually triggered)
- Normal schedule: 1 cleanup every 5 minutes

### Cleanup Not Deleting Notifications

**Verify expiration dates:**
```sql
SELECT COUNT(*) 
FROM "UserNotifications" 
WHERE "ExpiresAt" IS NOT NULL 
  AND "ExpiresAt" < NOW();
```

**Check user preferences:**
- Notification expiration defaults (30 days for most types)
- User-specific expiration settings

## Migration from Old System

### Old System (Removed)
```csharp
// Before: Cleanup on every call
public async Task<List<UserNotification>> GetUserNotificationsAsync(...)
{
    await DeleteExpiredNotificationsAsync();  // ❌ Removed
    // ... rest of method
}
```

### New System
```csharp
// After: Background service handles cleanup
public async Task<List<UserNotification>> GetUserNotificationsAsync(...)
{
    // No cleanup call - background service handles this ✅
    // ... rest of method
}
```

## Future Enhancements (Optional)

1. **Configurable Intervals**
   - Move intervals to appsettings.json
   - Allow runtime configuration changes

2. **Metrics Collection**
   - Track cleanup counts over time
   - Monitor average execution time
   - Alert on failures

3. **Smart Scheduling**
   - Run during low-traffic periods
   - Adjust frequency based on deletion counts

4. **User-Specific Cleanup**
   - Option to clean only active users
   - Background job for inactive users

## Related Documentation

- Notification System: `NOTIFICATIONS.md`
- User Preferences: `docs/USER_PREFERENCES.md`
- Background Services: [Microsoft Docs - Background tasks](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

## Code Locations

**NotificationCleanupService**
- File: `FreeSpeakWeb/Services/NotificationCleanupService.cs`
- Registered: `FreeSpeakWeb/Program.cs` (line ~90)

**NotificationService**
- File: `FreeSpeakWeb/Services/NotificationService.cs`
- Method: `DeleteExpiredNotificationsAsync()` (line ~336)

**Database Configuration**
- File: `FreeSpeakWeb/Data/ApplicationDbContext.cs`
- UserNotification entity configuration with indexes
