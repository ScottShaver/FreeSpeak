# Testing Patterns and Best Practices

## Overview

FreeSpeak uses a comprehensive testing strategy with three test projects:
- **FreeSpeakWeb.Tests**: Unit tests for services and components
- **FreeSpeakWeb.IntegrationTests**: Integration tests with real PostgreSQL via Testcontainers
- **FreeSpeakWeb.PerformanceTests**: Performance benchmarks with BenchmarkDotNet

## Testing Stack

### Unit Testing
- **xUnit** (2.9.3): Test framework
- **bUnit** (1.35.3): Blazor component testing
- **Moq** (4.20.72): Mocking framework
- **FluentAssertions** (7.0.0): Fluent assertion API
- **Microsoft.EntityFrameworkCore.InMemory** (10.0.0): In-memory database for testing

### Integration Testing
- **Testcontainers.PostgreSql** (4.1.0): Docker-based PostgreSQL for real database tests

### Performance Testing
- **BenchmarkDotNet** (0.14.0): Performance benchmarking

## Project Structure

```
FreeSpeakWeb.Tests/
├── Components/                    # Blazor component tests
│   ├── FeedArticleTests.cs
│   ├── MultiLineCommentDisplayTests.cs
│   └── PostEditModalTests.cs
├── Services/                      # Service layer tests
│   ├── PostServiceTests.cs
│   ├── GroupPostServiceTests.cs
│   ├── FriendsServiceTests.cs
│   ├── HtmlSanitizationServiceTests.cs
│   └── DeleteOperationTests.cs
├── Helpers/                       # Helper class tests
│   └── CommentHelpersTests.cs
└── Infrastructure/                # Test infrastructure
    ├── TestBase.cs                # Base class for all tests
    ├── TestDataFactory.cs         # Test data creation
    ├── TestRepositoryFactory.cs   # Repository creation for tests
    └── MockRepositories.cs        # Mock repository implementations
```

## Test Infrastructure

### TestBase Class

All test classes inherit from `TestBase` which provides common test utilities.

**Location**: `FreeSpeakWeb.Tests/Infrastructure/TestBase.cs`

```csharp
public abstract class TestBase : IDisposable
{
    // Creates an in-memory database context with a unique name
    protected ApplicationDbContext CreateInMemoryContext(string databaseName = "");

    // Creates a DbContext factory for services that use IDbContextFactory
    protected IDbContextFactory<ApplicationDbContext> CreateDbContextFactory(string databaseName = "");

    // Creates a TestRepositoryFactory with repositories configured for testing
    protected TestRepositoryFactory CreateTestRepositoryFactory(string databaseName = "");

    // Creates a mock logger for any service
    protected ILogger<T> CreateMockLogger<T>();
}
```

### TestDataFactory

Provides factory methods for creating test entities with sensible defaults.

**Location**: `FreeSpeakWeb.Tests/Infrastructure/TestDataFactory.cs`

```csharp
// Create a test user
var user = TestDataFactory.CreateTestUser(
    id: "user123",
    userName: "testuser",
    email: "test@example.com"
);

// Create a test post
var post = TestDataFactory.CreateTestPost(
    authorId: user.Id,
    content: "Test post content",
    audienceType: AudienceType.Public
);

// Create a test comment
var comment = TestDataFactory.CreateTestComment(
    postId: post.Id,
    authorId: user.Id,
    content: "Test comment"
);

// Create a test friendship
var friendship = TestDataFactory.CreateTestFriendship(
    requesterId: user1.Id,
    addresseeId: user2.Id,
    status: FriendshipStatus.Accepted
);
```

### TestRepositoryFactory

Creates repository instances configured for testing with in-memory database.

**Location**: `FreeSpeakWeb.Tests/Infrastructure/TestRepositoryFactory.cs`

```csharp
var repoFactory = CreateTestRepositoryFactory();

// Get any repository you need
var postRepo = repoFactory.CreateFeedPostRepository();
var commentRepo = repoFactory.CreateFeedCommentRepository();
var friendshipRepo = repoFactory.CreateFriendshipRepository();
```

## Unit Test Patterns

### Pattern 1: Service Test with Repository Pattern

**Example**: Testing PostService

```csharp
public class PostServiceTests : TestBase
{
    [Fact]
    public async Task CreatePostAsync_ValidPost_CreatesPostSuccessfully()
    {
        // Arrange
        var repoFactory = CreateTestRepositoryFactory();
        var postService = CreatePostService(repoFactory);

        var author = TestDataFactory.CreateTestUser();
        using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
        await context.Users.AddAsync(author);
        await context.SaveChangesAsync();

        // Act
        var result = await postService.CreatePostAsync(
            author.Id,
            "Test content",
            AudienceType.Public,
            null
        );

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be("Test content");
        result.AuthorId.Should().Be(author.Id);
        result.AudienceType.Should().Be(AudienceType.Public);
    }
}
```

**Key Points:**
- Inherit from `TestBase`
- Use `TestDataFactory` for creating test entities
- Use `TestRepositoryFactory` for real repository implementations
- Use FluentAssertions for readable assertions
- Follow Arrange-Act-Assert pattern

### Pattern 2: Testing with Custom Configuration

**Example**: Testing with custom site settings

```csharp
[Fact]
public async Task AddCommentAsync_ExceedsMaxDirectComments_ThrowsException()
{
    // Arrange
    var repoFactory = CreateTestRepositoryFactory();

    // Create service with custom setting for max comments
    var postService = CreatePostServiceWithCustomSettings(
        repoFactory,
        maxDirectComments: 2  // Only allow 2 direct comments
    );

    var author = TestDataFactory.CreateTestUser();
    var post = TestDataFactory.CreateTestPost(author.Id);

    using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
    await context.Users.AddAsync(author);
    await context.Posts.AddAsync(post);
    await context.SaveChangesAsync();

    // Add 2 comments (max allowed)
    await postService.AddCommentAsync(post.Id, author.Id, "Comment 1", null);
    await postService.AddCommentAsync(post.Id, author.Id, "Comment 2", null);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => postService.AddCommentAsync(post.Id, author.Id, "Comment 3", null)
    );

    exception.Message.Should().Contain("maximum number of direct comments");
}
```

### Pattern 3: Testing Business Logic with Multiple Entities

**Example**: Testing friend visibility logic

```csharp
[Fact]
public async Task GetUserFeedPostsAsync_FriendsOnlyPost_OnlyVisibleToFriends()
{
    // Arrange
    var repoFactory = CreateTestRepositoryFactory();
    var postService = CreatePostService(repoFactory);

    var author = TestDataFactory.CreateTestUser(id: "author");
    var friend = TestDataFactory.CreateTestUser(id: "friend");
    var stranger = TestDataFactory.CreateTestUser(id: "stranger");

    var friendship = TestDataFactory.CreateTestFriendship(
        author.Id,
        friend.Id,
        FriendshipStatus.Accepted
    );

    var friendsOnlyPost = TestDataFactory.CreateTestPost(
        author.Id,
        content: "Friends only post",
        audienceType: AudienceType.FriendsOnly
    );

    using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
    await context.Users.AddRangeAsync(author, friend, stranger);
    await context.Friendships.AddAsync(friendship);
    await context.Posts.AddAsync(friendsOnlyPost);
    await context.SaveChangesAsync();

    // Act - Friend should see the post
    var friendFeed = await postService.GetUserFeedPostsAsync(friend.Id, 0, 10);

    // Act - Stranger should NOT see the post
    var strangerFeed = await postService.GetUserFeedPostsAsync(stranger.Id, 0, 10);

    // Assert
    friendFeed.Should().ContainSingle(p => p.Id == friendsOnlyPost.Id);
    strangerFeed.Should().BeEmpty();
}
```

### Pattern 4: Testing with Database State Verification

**Example**: Verifying cascade operations

```csharp
[Fact]
public async Task DeletePostAsync_WithComments_DeletesAllRelatedData()
{
    // Arrange
    var repoFactory = CreateTestRepositoryFactory();
    var postService = CreatePostService(repoFactory);

    var author = TestDataFactory.CreateTestUser();
    var post = TestDataFactory.CreateTestPost(author.Id);

    using var setupContext = await repoFactory.ContextFactory.CreateDbContextAsync();
    await setupContext.Users.AddAsync(author);
    await setupContext.Posts.AddAsync(post);
    await setupContext.SaveChangesAsync();

    var postId = post.Id;

    // Add comments and likes
    var comment = TestDataFactory.CreateTestComment(postId, author.Id);
    var like = TestDataFactory.CreateTestLike(postId, author.Id);

    await setupContext.Comments.AddAsync(comment);
    await setupContext.Likes.AddAsync(like);
    await setupContext.SaveChangesAsync();

    // Act
    await postService.DeletePostAsync(postId, author.Id);

    // Assert - Verify all related data is deleted
    using var verifyContext = await repoFactory.ContextFactory.CreateDbContextAsync();
    var deletedPost = await verifyContext.Posts.FindAsync(postId);
    var remainingComments = await verifyContext.Comments
        .Where(c => c.PostId == postId)
        .ToListAsync();
    var remainingLikes = await verifyContext.Likes
        .Where(l => l.PostId == postId)
        .ToListAsync();

    deletedPost.Should().BeNull();
    remainingComments.Should().BeEmpty();
    remainingLikes.Should().BeEmpty();
}
```

## Component Testing with bUnit

### Pattern 5: Testing Blazor Components

**Example**: Testing FeedArticle component

```csharp
public class FeedArticleTests : TestContext
{
    [Fact]
    public void FeedArticle_RendersPostContent()
    {
        // Arrange
        var user = TestDataFactory.CreateTestUser();
        var post = new PostViewModel
        {
            Id = 1,
            Content = "Test post content",
            AuthorId = user.Id,
            AuthorUserName = user.UserName,
            AuthorProfilePictureUrl = "/images/default-avatar.png",
            CreatedAt = DateTime.UtcNow,
            LikeCount = 5,
            CommentCount = 3
        };

        // Register required services
        Services.AddSingleton<IOptions<SiteSettings>>(
            Options.Create(new SiteSettings { SiteName = "TestSite" })
        );

        // Act
        var cut = RenderComponent<FeedArticle>(parameters => parameters
            .Add(p => p.Post, post)
        );

        // Assert
        cut.Find(".post-content").TextContent.Should().Contain("Test post content");
        cut.Find(".author-name").TextContent.Should().Contain(user.UserName);
        cut.Find(".like-count").TextContent.Should().Contain("5");
        cut.Find(".comment-count").TextContent.Should().Contain("3");
    }
}
```

## Testing Best Practices

### 1. Test Naming Convention

Use descriptive test names that follow the pattern:
```
MethodName_Scenario_ExpectedBehavior
```

Examples:
- `CreatePostAsync_ValidPost_CreatesPostSuccessfully`
- `AddCommentAsync_ExceedsMaxDirectComments_ThrowsException`
- `GetUserFeedPostsAsync_FriendsOnlyPost_OnlyVisibleToFriends`

### 2. Arrange-Act-Assert Pattern

Always structure tests in three clear sections:

```csharp
[Fact]
public async Task TestMethod()
{
    // Arrange - Set up test data and dependencies
    var user = TestDataFactory.CreateTestUser();

    // Act - Execute the code under test
    var result = await service.DoSomething(user.Id);

    // Assert - Verify the expected outcome
    result.Should().NotBeNull();
}
```

### 3. Use Fluent Assertions

FluentAssertions provides more readable and descriptive assertions:

```csharp
// Instead of
Assert.Equal(5, list.Count);
Assert.True(result.IsSuccess);
Assert.NotNull(user);

// Use
list.Should().HaveCount(5);
result.IsSuccess.Should().BeTrue();
user.Should().NotBeNull();
```

### 4. Test Isolation

Each test should be independent and not rely on other tests:

```csharp
// Good - Each test creates its own data
[Fact]
public async Task Test1()
{
    var repoFactory = CreateTestRepositoryFactory("test1");
    // Test with isolated database
}

[Fact]
public async Task Test2()
{
    var repoFactory = CreateTestRepositoryFactory("test2");
    // Different isolated database
}
```

### 5. Test One Thing at a Time

Each test should verify a single behavior:

```csharp
// Bad - Testing multiple things
[Fact]
public async Task CreateAndDeletePost()
{
    var post = await service.CreatePostAsync(...);
    post.Should().NotBeNull();

    await service.DeletePostAsync(post.Id);
    var deleted = await service.GetPostAsync(post.Id);
    deleted.Should().BeNull();
}

// Good - Separate tests
[Fact]
public async Task CreatePost_ValidData_CreatesSuccessfully()
{
    var post = await service.CreatePostAsync(...);
    post.Should().NotBeNull();
}

[Fact]
public async Task DeletePost_ExistingPost_RemovesFromDatabase()
{
    // Setup post
    await service.DeletePostAsync(post.Id);
    var deleted = await service.GetPostAsync(post.Id);
    deleted.Should().BeNull();
}
```

### 6. Use Theory for Parameterized Tests

Test multiple scenarios with a single test method:

```csharp
[Theory]
[InlineData(AudienceType.Public)]
[InlineData(AudienceType.FriendsOnly)]
[InlineData(AudienceType.Private)]
public async Task CreatePost_WithDifferentAudienceTypes_SavesCorrectly(
    AudienceType audienceType)
{
    // Arrange
    var repoFactory = CreateTestRepositoryFactory();
    var postService = CreatePostService(repoFactory);
    var author = TestDataFactory.CreateTestUser();

    // Act
    var post = await postService.CreatePostAsync(
        author.Id,
        "Test content",
        audienceType,
        null
    );

    // Assert
    post.AudienceType.Should().Be(audienceType);
}
```

### 7. Test Boundary Conditions

Always test edge cases and limits:

```csharp
[Fact]
public async Task AddComment_AtExactMaxDepth_Succeeds()
{
    // Test at the maximum allowed depth
}

[Fact]
public async Task AddComment_ExceedsMaxDepth_ThrowsException()
{
    // Test one over the limit
}

[Fact]
public async Task AddComment_EmptyContent_ThrowsException()
{
    // Test empty/null content
}
```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test FreeSpeakWeb.Tests
dotnet test FreeSpeakWeb.IntegrationTests
dotnet test FreeSpeakWeb.PerformanceTests
```

### Run Tests with Code Coverage
```bash
dotnet test /p:CollectCoverage=true
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~PostServiceTests"
```

### Run Specific Test Method
```bash
dotnet test --filter "FullyQualifiedName~PostServiceTests.CreatePostAsync_ValidPost_CreatesPostSuccessfully"
```

## Integration Testing

Integration tests use Testcontainers to spin up real PostgreSQL instances:

```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    private PostgreSqlContainer _dbContainer;

    public async Task InitializeAsync()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .Build();

        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}
```

## Performance Testing

Performance tests use BenchmarkDotNet to measure execution time and memory:

```csharp
[MemoryDiagnoser]
public class QueryPerformanceBenchmarks
{
    [Benchmark]
    public async Task<List<Post>> GetPostsWithTracking()
    {
        return await context.Posts.ToListAsync();
    }

    [Benchmark]
    public async Task<List<Post>> GetPostsWithoutTracking()
    {
        return await context.Posts.AsNoTracking().ToListAsync();
    }
}
```

## Continuous Integration

Tests run automatically on:
- Pull requests
- Commits to main branch
- Nightly builds

## Related Documentation

- [Repository Pattern Guide](REPOSITORY_PATTERN.md)
- [Caching Strategy](CACHING.md)
- [Development Guidelines](DEVELOPER_GUIDE_BASE_COMPONENTS.md)
