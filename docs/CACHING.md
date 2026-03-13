# Caching Strategy and Implementation

## Overview

FreeSpeak implements a multi-layered caching strategy to optimize database queries and improve application performance. The caching system supports both single-server deployments (in-memory caching) and multi-server deployments (Redis distributed caching).

## Key Performance Improvements

- **Friendship Cache**: 80%+ performance improvement for friend list queries
- **Compiled Queries**: 10-20% faster database query execution
- **Cache Stampede Prevention**: Thread-safe locking prevents duplicate queries
- **DTO Projections**: Reduced memory usage and faster data transfer

## Caching Architecture

### 1. Distributed Cache Service (`DistributedCacheService`)

The distributed cache service provides a unified interface for caching that works with both Redis and in-memory implementations.

**Location**: `FreeSpeakWeb/Services/DistributedCacheService.cs`

**Interface**: `ICacheService`

#### Configuration

Configure caching in `appsettings.json`:

```json
{
  "Caching": {
    "UseRedis": false,
    "RedisConnectionString": "localhost:6379,abortConnect=false"
  }
}
```

**Settings:**
- `UseRedis`: Set to `true` to enable Redis, `false` for in-memory caching
- `RedisConnectionString`: Redis server connection string (required when UseRedis is true)

#### Features

**Type-Safe Caching**
```csharp
// Get a cached value
var friendIds = await _cacheService.GetAsync<List<string>>("user_friends_123");

// Set a cached value with expiration
await _cacheService.SetAsync("user_friends_123", friendIds, new CacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    SlidingExpiration = TimeSpan.FromMinutes(2)
});
```

**Cache Stampede Prevention**
```csharp
// GetOrCreateAsync prevents multiple threads from executing the factory simultaneously
var friendIds = await _cacheService.GetOrCreateAsync(
    key: $"user_friends_{userId}",
    factory: async () => await LoadFriendIdsFromDatabase(userId),
    options: CacheEntryOptions.Default
);
```

**Pattern-Based Invalidation**
```csharp
// Remove all friend-related cache entries
await _cacheService.RemoveByPatternAsync("user_friends_*");
```

#### API Methods

| Method | Description |
|--------|-------------|
| `GetAsync<T>(key)` | Retrieves a value from cache |
| `SetAsync<T>(key, value, options)` | Stores a value in cache with expiration |
| `RemoveAsync(key)` | Removes a specific cache entry |
| `GetOrCreateAsync<T>(key, factory, options)` | Gets cached value or creates it using factory |
| `RemoveByPatternAsync(pattern)` | Removes entries matching a pattern (wildcards supported) |
| `ExistsAsync(key)` | Checks if a key exists in cache |
| `RefreshAsync(key)` | Refreshes sliding expiration for an entry |

### 2. Friendship Cache Service (`FriendshipCacheService`)

Specialized caching service for friend lists, which change infrequently but are queried frequently.

**Location**: `FreeSpeakWeb/Services/FriendshipCacheService.cs`

#### Usage

**Getting Friend IDs**
```csharp
public class MyService
{
    private readonly FriendshipCacheService _friendshipCache;

    public async Task<List<Post>> GetFriendPostsAsync(string userId)
    {
        // This call is cached for 5 minutes
        var friendIds = await _friendshipCache.GetUserFriendIdsAsync(userId);

        // Query posts from friends
        var posts = await _context.Posts
            .Where(p => friendIds.Contains(p.AuthorId))
            .ToListAsync();

        return posts;
    }
}
```

**Cache Invalidation**
```csharp
// When friendship changes, invalidate both users' caches
_friendshipCache.InvalidateFriendshipCache(userId1, userId2);
```

#### Cache Strategy

- **Cache Duration**: 5 minutes absolute, 2 minutes sliding
- **Cache Key Pattern**: `user_friends_{userId}`
- **Invalidation**: Triggered on friendship status changes (accept, remove, block)

### 3. Compiled Queries (`CompiledQueries`)

EF Core compiled queries eliminate query compilation overhead on repeated executions.

**Location**: `FreeSpeakWeb/Data/CompiledQueries.cs`

#### Available Compiled Queries

**Post Queries**
- `GetPostByIdAsync(context, postId)` - Get post with author and images
- `PostExistsAsync(context, postId)` - Check if post exists
- `GetPostsByAuthorAsync(context, authorId, skip, take)` - Get paginated posts by author
- `GetPostCountByAuthorAsync(context, authorId)` - Count posts by author
- `GetPublicPostsAsync(context, skip, take)` - Get paginated public posts

**Group Post Queries**
- `GetGroupPostByIdAsync(context, postId)` - Get group post with details
- `GroupPostExistsAsync(context, postId)` - Check if group post exists
- `GetGroupPostsByGroupAsync(context, groupId, skip, take)` - Get paginated group posts

#### Usage Example

```csharp
// Instead of writing the query inline
var post = await _context.Posts
    .Include(p => p.Author)
    .Include(p => p.Images)
    .FirstOrDefaultAsync(p => p.Id == postId);

// Use the compiled query
var post = await CompiledQueries.GetPostByIdAsync(_context, postId);
```

**Benefits:**
- 10-20% faster query execution
- Reduced memory allocations
- Predictable performance

## Query Optimization Patterns

### 1. AsNoTracking

Use `AsNoTracking()` for read-only queries to avoid change tracking overhead:

```csharp
var posts = await _context.Posts
    .AsNoTracking()  // No change tracking = faster queries
    .Include(p => p.Author)
    .ToListAsync();
```

**When to Use:**
- Read-only queries
- Queries for display purposes
- Bulk data retrieval

**When NOT to Use:**
- When you need to update entities
- When you need change tracking for auditing

### 2. AsSplitQuery

Use `AsSplitQuery()` when including multiple collections to avoid cartesian explosion:

```csharp
var posts = await _context.Posts
    .AsNoTracking()
    .AsSplitQuery()  // Splits into multiple SQL queries
    .Include(p => p.Images)
    .Include(p => p.Comments)
    .ToListAsync();
```

**Benefits:**
- Prevents duplicate data from JOINs
- Reduces memory usage
- Faster for large result sets

### 3. DTO Projections

Select only the data you need using projections:

```csharp
// Instead of loading the entire entity
var posts = await _context.Posts
    .Include(p => p.Author)
    .Include(p => p.Images)
    .ToListAsync();

// Project to a DTO with only required fields
var posts = await _context.Posts
    .AsNoTracking()
    .Select(p => new PostListDto
    {
        Id = p.Id,
        Content = p.Content,
        AuthorName = p.Author.UserName,
        ImageCount = p.Images.Count,
        CreatedAt = p.CreatedAt
    })
    .ToListAsync();
```

**Benefits:**
- Reduced data transfer
- Lower memory usage
- Faster serialization
- Network bandwidth savings

## Performance Monitoring

### Query Performance Logger

Use `QueryPerformanceLogger` to monitor slow queries:

**Location**: `FreeSpeakWeb/Services/QueryPerformanceLogger.cs`

```csharp
public class MyService
{
    private readonly QueryPerformanceLogger _perfLogger;

    public async Task<List<Post>> GetPostsAsync()
    {
        using var timer = _perfLogger.StartTimer("GetPosts");

        var posts = await _context.Posts.ToListAsync();

        // Automatically logs if query takes > 100ms
        return posts;
    }
}
```

**Features:**
- Automatic logging of slow queries (> 100ms)
- Operation name tracking
- Execution time measurement
- Integration with ASP.NET Core logging

## Best Practices

### 1. Cache Key Naming

Use consistent, descriptive cache key patterns:

```csharp
// Good: Descriptive and includes entity type and ID
$"user_friends_{userId}"
$"post_comments_{postId}"
$"group_members_{groupId}"

// Bad: Ambiguous or too generic
$"cache_{id}"
$"data"
```

### 2. Cache Expiration Strategy

Choose expiration based on data volatility:

| Data Type | Absolute Expiration | Sliding Expiration | Rationale |
|-----------|---------------------|-------------------|-----------|
| Friend Lists | 5 minutes | 2 minutes | Changes infrequently, queried often |
| Post Counts | 10 minutes | 5 minutes | Can tolerate slight staleness |
| User Profiles | 15 minutes | 10 minutes | Rarely changes |
| Real-time Data | 30 seconds | 15 seconds | Needs to be fresh |

### 3. Cache Invalidation

Always invalidate cache when underlying data changes:

```csharp
public async Task AcceptFriendRequestAsync(string userId1, string userId2)
{
    // Update database
    await UpdateFriendshipStatus(userId1, userId2, FriendshipStatus.Accepted);

    // Invalidate cache for both users
    _friendshipCache.InvalidateFriendshipCache(userId1, userId2);
}
```

### 4. Defensive Caching

Handle cache failures gracefully:

```csharp
try
{
    var data = await _cacheService.GetAsync<MyData>(cacheKey);
    if (data != null)
        return data;
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Cache retrieval failed, falling back to database");
}

// Always fall back to database
return await LoadFromDatabase();
```

## Deployment Considerations

### Single-Server Deployment

**Configuration:**
```json
{
  "Caching": {
    "UseRedis": false
  }
}
```

**Characteristics:**
- In-memory caching (IMemoryCache)
- No external dependencies
- Cache is per-application instance
- Suitable for development and small deployments

### Multi-Server Deployment (Redis)

**Configuration:**
```json
{
  "Caching": {
    "UseRedis": true,
    "RedisConnectionString": "your-redis-server:6379,password=yourpassword,ssl=true,abortConnect=false"
  }
}
```

**Requirements:**
- Redis server (6.0 or higher recommended)
- Network connectivity between app servers and Redis
- Consider Redis Cluster for high availability

**Connection String Options:**
- `password`: Redis authentication password
- `ssl`: Enable SSL/TLS encryption
- `abortConnect`: Set to `false` to allow reconnection on connection failure
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Synchronous operation timeout in milliseconds

### Redis Best Practices

1. **Use Connection Pooling**: StackExchange.Redis handles this automatically
2. **Enable SSL in Production**: Protect data in transit
3. **Set Appropriate Timeouts**: Prevent hanging connections
4. **Monitor Redis Memory**: Set maxmemory policy (allkeys-lru recommended)
5. **Use Redis Sentinel or Cluster**: For high availability

## Troubleshooting

### Cache Miss Too Often

**Symptoms**: High database load, slow response times

**Solutions:**
1. Increase cache duration
2. Check if cache is being invalidated too frequently
3. Verify cache keys are consistent
4. Check Redis connectivity (if using Redis)

### Memory Usage Too High

**Symptoms**: Application using too much memory

**Solutions:**
1. Reduce cache duration
2. Use smaller DTOs in cached objects
3. Implement cache size limits
4. Review what's being cached

### Stale Data Issues

**Symptoms**: Users see outdated information

**Solutions:**
1. Reduce cache duration
2. Implement proper cache invalidation
3. Use sliding expiration for frequently accessed data
4. Consider event-based cache invalidation

### Redis Connection Failures

**Symptoms**: Exceptions logged, degraded performance

**Solutions:**
1. Check Redis server status
2. Verify connection string is correct
3. Ensure network connectivity
4. Check Redis maxclients setting
5. Review Redis logs for errors

## Related Documentation

- [Performance Optimization Guide](PERFORMANCE_OPTIMIZATION.md)
- [Repository Pattern Guide](REPOSITORY_PATTERN.md)
- [Configuration Guide](CONFIGURATION.md)
- [Testing Patterns](TESTING_PATTERNS.md)
