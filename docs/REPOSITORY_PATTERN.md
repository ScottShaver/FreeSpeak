# Repository Pattern Guide

## Overview

FreeSpeak implements the Repository Pattern to abstract data access logic and provide a clean separation between business logic (services) and data persistence (Entity Framework Core). This pattern improves testability, maintainability, and allows for easier database provider changes.

## Benefits

1. **Abstraction**: Services don't directly depend on DbContext, making code more maintainable
2. **Testability**: Easy to mock repositories for unit testing
3. **Centralization**: Data access logic is centralized in one place
4. **Consistency**: Common operations are standardized across entities
5. **Flexibility**: Easy to swap database providers or add caching layers

## Architecture

```
Services (Business Logic)
    ↓
Repository Interfaces (Abstractions)
    ↓
Repository Implementations
    ↓
Entity Framework Core (DbContext)
    ↓
PostgreSQL Database
```

## Repository Structure

### Base Repository Interface

**Location**: `FreeSpeakWeb/Repositories/Abstractions/IRepository.cs`

Provides common CRUD operations for all entities:

```csharp
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(int id);
    Task<List<TEntity>> GetAllAsync();
    Task<TEntity> AddAsync(TEntity entity);
    Task UpdateAsync(TEntity entity);
    Task DeleteAsync(TEntity entity);
    Task<bool> ExistsAsync(int id);
}
```

### Specialized Repository Interfaces

Domain-specific repositories extend the base interface with specialized operations:

#### Post Repositories

**IFeedPostRepository** - Feed posts (e.g., `Post` entity)
- `GetByIdAsync(postId, includeAuthor, includeImages)`
- `GetByAuthorAsync(authorId, skip, take)`
- `GetPublicPostsAsync(skip, take)`
- `GetFriendPostsAsync(authorIds, skip, take)`

**IGroupPostRepository** - Group posts (e.g., `GroupPost` entity)
- `GetByGroupAsync(groupId, skip, take)`
- `GetPinnedPostsAsync(groupId)`
- `GetByAuthorInGroupAsync(groupId, authorId, skip, take)`

#### Comment Repositories

**IFeedCommentRepository** - Feed post comments
- `GetByPostAsync(postId)`
- `GetByIdWithAuthorAsync(commentId)`
- `CountDirectCommentsByPostAsync(postId)`

**IGroupCommentRepository** - Group post comments
- Similar operations for group posts

#### Like Repositories

**IFeedPostLikeRepository** - Feed post likes
- `GetByPostAsync(postId)`
- `GetByUserAndPostAsync(userId, postId)`
- `CountByPostAsync(postId)`

**IGroupPostLikeRepository** - Group post likes
**IFeedCommentLikeRepository** - Comment likes
**IGroupCommentLikeRepository** - Group comment likes

#### Domain Repositories

**IFriendshipRepository** - Friend relationships
- `GetAcceptedFriendshipsAsync(userId)`
- `GetPendingRequestsAsync(userId)`
- `GetFriendshipAsync(userId1, userId2)`

**IGroupRepository** - Group management
- `GetByIdAsync(groupId, includeMembers)`
- `GetUserGroupsAsync(userId)`
- `SearchGroupsAsync(searchTerm, skip, take)`

**IUserRepository** - User operations
- `GetByIdAsync(userId)`
- `SearchUsersAsync(searchTerm, skip, take)`

**INotificationRepository** - User notifications
- `GetByUserAsync(userId, skip, take)`
- `GetUnreadCountAsync(userId)`
- `MarkAsReadAsync(notificationId)`

**IPinnedPostRepository** - Pinned posts management
**IPostNotificationMuteRepository** - Notification muting
**IGroupMemberRepository** - Group membership

## Base Post Repository

To reduce code duplication between `PostRepository` and `GroupPostRepository`, a generic base class provides common functionality.

**Location**: `FreeSpeakWeb/Repositories/BasePostRepository.cs`

```csharp
public abstract class BasePostRepository<TPost, TImage, TContext>
    where TPost : class, IPostEntity
    where TImage : class, IPostImage
    where TContext : DbContext
{
    // Common operations implemented once
    protected Task<TPost?> GetByIdInternalAsync(int postId, bool includeAuthor, bool includeImages);
    protected Task<bool> ExistsInternalAsync(int postId);
    protected Task<List<TPost>> GetByAuthorInternalAsync(string authorId, int skip, int take);
    protected Task<int> CountByAuthorInternalAsync(string authorId);
    protected Task<TPost> AddInternalAsync(TPost post);
    protected Task UpdateInternalAsync(TPost post);
    protected Task DeleteInternalAsync(int postId);
}
```

### Concrete Implementation Example

```csharp
public class PostRepository : BasePostRepository<Post, PostImage, ApplicationDbContext>,
    IFeedPostRepository<Post, PostImage>
{
    public PostRepository(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<PostRepository> logger)
        : base(contextFactory, logger)
    {
    }

    // Implement abstract members
    protected override DbSet<Post> GetPostSet(ApplicationDbContext context)
        => context.Posts;

    protected override DbSet<PostImage> GetImageSet(ApplicationDbContext context)
        => context.PostImages;

    protected override IQueryable<Post> CreateBaseQuery(
        ApplicationDbContext context,
        bool includeAuthor,
        bool includeImages)
    {
        var query = context.Posts.AsNoTracking();

        if (includeAuthor)
            query = query.Include(p => p.Author);

        if (includeImages)
            query = query.Include(p => p.Images.OrderBy(i => i.DisplayOrder));

        return query;
    }

    // Implement interface methods by calling base methods
    public Task<Post?> GetByIdAsync(int postId, bool includeAuthor = true, bool includeImages = true)
        => GetByIdInternalAsync(postId, includeAuthor, includeImages);

    // Add specialized methods
    public async Task<List<Post>> GetPublicPostsAsync(int skip, int take)
    {
        using var context = await ContextFactory.CreateDbContextAsync();
        return await CreateBaseQuery(context, true, true)
            .Where(p => p.AudienceType == AudienceType.Public)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
}
```

## Usage in Services

Services depend on repository interfaces, not implementations:

```csharp
public class PostService
{
    private readonly IFeedPostRepository<Post, PostImage> _postRepository;
    private readonly IFeedCommentRepository _commentRepository;
    private readonly IFeedPostLikeRepository _likeRepository;

    public PostService(
        IFeedPostRepository<Post, PostImage> postRepository,
        IFeedCommentRepository commentRepository,
        IFeedPostLikeRepository likeRepository)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _likeRepository = likeRepository;
    }

    public async Task<Post?> GetPostAsync(int postId)
    {
        // Use repository instead of DbContext directly
        return await _postRepository.GetByIdAsync(postId);
    }

    public async Task<Post> CreatePostAsync(
        string authorId,
        string content,
        AudienceType audienceType,
        List<string>? imageUrls)
    {
        var post = new Post
        {
            AuthorId = authorId,
            Content = content,
            AudienceType = audienceType,
            CreatedAt = DateTime.UtcNow
        };

        // Repository handles the database operation
        return await _postRepository.AddAsync(post);
    }
}
```

## Dependency Injection Configuration

Repositories are registered in `Program.cs`:

```csharp
// Post repositories
builder.Services.AddScoped<IFeedPostRepository<Post, PostImage>, PostRepository>();
builder.Services.AddScoped<IGroupPostRepository<GroupPost, GroupPostImage>, GroupPostRepository>();

// Comment repositories
builder.Services.AddScoped<IFeedCommentRepository, FeedCommentRepository>();
builder.Services.AddScoped<IGroupCommentRepository, GroupCommentRepository>();

// Like repositories
builder.Services.AddScoped<IFeedPostLikeRepository, FeedPostLikeRepository>();
builder.Services.AddScoped<IGroupPostLikeRepository, GroupPostLikeRepository>();
builder.Services.AddScoped<IFeedCommentLikeRepository, FeedCommentLikeRepository>();
builder.Services.AddScoped<IGroupCommentLikeRepository, GroupCommentLikeRepository>();

// Domain repositories
builder.Services.AddScoped<IFriendshipRepository, FriendshipRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IPinnedPostRepository, PinnedPostRepository>();
builder.Services.AddScoped<IPostNotificationMuteRepository, PostNotificationMuteRepository>();
builder.Services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
```

## Testing with Repositories

The repository pattern makes testing much easier:

### Using Real Repositories with In-Memory Database

```csharp
[Fact]
public async Task CreatePost_SavesCorrectly()
{
    // Arrange
    var repoFactory = CreateTestRepositoryFactory();
    var postRepo = repoFactory.CreateFeedPostRepository();

    var author = TestDataFactory.CreateTestUser();
    var post = TestDataFactory.CreateTestPost(author.Id);

    // Act
    var savedPost = await postRepo.AddAsync(post);

    // Assert
    savedPost.Should().NotBeNull();
    savedPost.Id.Should().BeGreaterThan(0);
}
```

### Mocking Repositories

```csharp
[Fact]
public async Task GetPost_CallsRepository()
{
    // Arrange
    var mockRepo = new Mock<IFeedPostRepository<Post, PostImage>>();
    var expectedPost = TestDataFactory.CreateTestPost("author123");

    mockRepo.Setup(r => r.GetByIdAsync(1, true, true))
        .ReturnsAsync(expectedPost);

    var service = new PostService(mockRepo.Object, ...);

    // Act
    var result = await service.GetPostAsync(1);

    // Assert
    result.Should().Be(expectedPost);
    mockRepo.Verify(r => r.GetByIdAsync(1, true, true), Times.Once);
}
```

## Performance Optimizations in Repositories

### 1. AsNoTracking for Read-Only Queries

```csharp
public async Task<List<Post>> GetPublicPostsAsync(int skip, int take)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()  // No change tracking overhead
        .Include(p => p.Author)
        .Where(p => p.AudienceType == AudienceType.Public)
        .OrderByDescending(p => p.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

### 2. AsSplitQuery for Multiple Includes

```csharp
public async Task<Post?> GetByIdAsync(int postId, bool includeAuthor, bool includeImages)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()
        .AsSplitQuery()  // Prevents cartesian explosion
        .Include(p => p.Author)
        .Include(p => p.Images)
        .Include(p => p.Comments)
        .FirstOrDefaultAsync(p => p.Id == postId);
}
```

### 3. Projection to DTOs

```csharp
public async Task<List<PostListDto>> GetPostListAsync(int skip, int take)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()
        .Select(p => new PostListDto
        {
            Id = p.Id,
            Content = p.Content,
            AuthorName = p.Author.UserName,
            CreatedAt = p.CreatedAt,
            LikeCount = p.LikeCount,
            CommentCount = p.CommentCount
        })
        .OrderByDescending(p => p.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

### 4. Using Compiled Queries

```csharp
// In repository
public async Task<Post?> GetByIdAsync(int postId)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    // Use compiled query for 10-20% performance improvement
    return await CompiledQueries.GetPostByIdAsync(context, postId);
}
```

## Common Patterns

### Pattern 1: Pagination

```csharp
public async Task<List<Post>> GetPagedPostsAsync(int skip, int take)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()
        .OrderByDescending(p => p.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

### Pattern 2: Filtered Queries

```csharp
public async Task<List<Post>> GetPostsByAudienceAsync(
    AudienceType audienceType,
    int skip,
    int take)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()
        .Where(p => p.AudienceType == audienceType)
        .OrderByDescending(p => p.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

### Pattern 3: Counting

```csharp
public async Task<int> CountByAuthorAsync(string authorId)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .CountAsync(p => p.AuthorId == authorId);
}
```

### Pattern 4: Existence Checks

```csharp
public async Task<bool> ExistsAsync(int postId)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts.AnyAsync(p => p.Id == postId);
}
```

### Pattern 5: Complex Queries

```csharp
public async Task<List<Post>> GetFriendPostsAsync(
    List<string> friendIds,
    int skip,
    int take)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts
        .AsNoTracking()
        .AsSplitQuery()
        .Include(p => p.Author)
        .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
        .Where(p => friendIds.Contains(p.AuthorId) &&
                   p.AudienceType == AudienceType.FriendsOnly)
        .OrderByDescending(p => p.CreatedAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

## Best Practices

### 1. Use DbContextFactory

Always use `IDbContextFactory<ApplicationDbContext>` for creating contexts:

```csharp
// Good
using var context = await ContextFactory.CreateDbContextAsync();

// Bad - Don't inject DbContext directly in repositories
private readonly ApplicationDbContext _context; // Avoid this
```

**Why?** DbContext is not thread-safe. Factory pattern ensures each operation gets its own context.

### 2. Dispose Contexts Properly

Always use `using` statements:

```csharp
public async Task<Post?> GetByIdAsync(int postId)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    return await context.Posts.FindAsync(postId);
    // Context is automatically disposed
}
```

### 3. Handle Exceptions

Log and handle exceptions appropriately:

```csharp
public async Task<Post?> GetByIdAsync(int postId)
{
    try
    {
        using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Posts.FindAsync(postId);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error retrieving post {PostId}", postId);
        return null;
    }
}
```

### 4. Keep Repositories Focused

Repositories should only handle data access, not business logic:

```csharp
// Good - Simple data access
public async Task<Post> AddAsync(Post post)
{
    using var context = await ContextFactory.CreateDbContextAsync();
    context.Posts.Add(post);
    await context.SaveChangesAsync();
    return post;
}

// Bad - Business logic in repository
public async Task<Post> AddAsync(Post post)
{
    // Validation logic
    if (string.IsNullOrEmpty(post.Content))
        throw new ArgumentException("Content is required");

    // Business rules
    if (post.Content.Length > 5000)
        post.Content = post.Content.Substring(0, 5000);

    // This belongs in a service, not repository
    await SendNotificationsAsync(post);

    using var context = await ContextFactory.CreateDbContextAsync();
    context.Posts.Add(post);
    await context.SaveChangesAsync();
    return post;
}
```

### 5. Use Appropriate Return Types

Return `Task<T?>` for single items, `Task<List<T>>` for collections:

```csharp
// Single item - can be null
public async Task<Post?> GetByIdAsync(int postId);

// Collection - never null, empty list if no results
public async Task<List<Post>> GetAllAsync();

// Boolean checks
public async Task<bool> ExistsAsync(int postId);

// Counts
public async Task<int> CountAsync();
```

## Migration from Direct DbContext Usage

If you have services using DbContext directly, migrate gradually:

### Before (Direct DbContext)
```csharp
public class PostService
{
    private readonly ApplicationDbContext _context;

    public async Task<Post?> GetPostAsync(int postId)
    {
        return await _context.Posts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == postId);
    }
}
```

### After (Repository Pattern)
```csharp
public class PostService
{
    private readonly IFeedPostRepository<Post, PostImage> _postRepository;

    public async Task<Post?> GetPostAsync(int postId)
    {
        return await _postRepository.GetByIdAsync(postId);
    }
}
```

## Related Documentation

- [Caching Strategy](CACHING.md)
- [Testing Patterns](TESTING_PATTERNS.md)
- [Performance Optimization](PERFORMANCE_OPTIMIZATION.md)
