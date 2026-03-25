# FreeSpeak Testing Guide

## Overview

FreeSpeak uses a comprehensive testing strategy with three test projects:
- **FreeSpeakWeb.Tests**: Unit tests for services and components
- **FreeSpeakWeb.IntegrationTests**: Integration tests with real PostgreSQL via Testcontainers
- **FreeSpeakWeb.PerformanceTests**: Performance benchmarks with BenchmarkDotNet

All tests follow the **AAA (Arrange, Act, Assert)** pattern for clarity and maintainability.

## Testing Stack

| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.9.3 | Test framework |
| bUnit | 1.35.3 | Blazor component testing |
| Moq | 4.20.72 | Mocking framework |
| FluentAssertions | 7.0.0 | Fluent assertion API |
| EF Core InMemory | 10.0.0 | In-memory database |
| Testcontainers.PostgreSql | 4.1.0 | Docker-based PostgreSQL |
| BenchmarkDotNet | 0.14.0 | Performance benchmarking |

## Project Structure

```
FreeSpeakWeb.Tests/
├── Infrastructure/                # Test infrastructure
│   ├── TestBase.cs                # Base class for all tests
│   ├── TestDataFactory.cs         # Test data creation
│   ├── TestRepositoryFactory.cs   # Repository creation for tests
│   └── MockRepositories.cs        # Mock repository implementations
├── Services/                      # Service layer tests
│   ├── PostServiceTests.cs
│   ├── GroupPostServiceTests.cs
│   ├── FriendsServiceTests.cs
│   ├── HtmlSanitizationServiceTests.cs
│   ├── NotificationServiceTests.cs
│   └── DeleteOperationTests.cs
├── Components/                    # Blazor component tests
│   ├── FeedArticleTests.cs
│   ├── MultiLineCommentDisplayTests.cs
│   ├── PostEditModalTests.cs
│   └── NotificationComponentTests.cs
└── Helpers/                       # Helper class tests
    └── CommentHelpersTests.cs
```

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run tests with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~PostServiceTests"
```

### Run tests in watch mode (during development)
```bash
dotnet watch test
```

### Run tests by category
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

## Test Infrastructure

### TestBase Class

All test classes inherit from `TestBase` which provides common test utilities:

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

Factory methods for creating test entities with sensible defaults:

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

Creates repository instances configured for testing:

```csharp
var repoFactory = CreateTestRepositoryFactory();

// Get any repository you need
var postRepo = repoFactory.CreateFeedPostRepository();
var commentRepo = repoFactory.CreateFeedCommentRepository();
var friendshipRepo = repoFactory.CreateFriendshipRepository();
```

## Writing Tests

### Service Tests (xUnit)

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
    }
}
```

### Component Tests (bUnit)

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
            AuthorUserName = user.UserName,
            CreatedAt = DateTime.UtcNow,
            LikeCount = 5,
            CommentCount = 3
        };

        Services.AddSingleton<IOptions<SiteSettings>>(
            Options.Create(new SiteSettings { SiteName = "TestSite" })
        );

        // Act
        var cut = RenderComponent<FeedArticle>(parameters => parameters
            .Add(p => p.Post, post));

        // Assert
        cut.Find(".post-content").TextContent.Should().Contain("Test post content");
        cut.Find(".like-count").TextContent.Should().Contain("5");
    }
}
```

### Testing Business Logic with Multiple Entities

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
        author.Id, friend.Id, FriendshipStatus.Accepted);

    var friendsOnlyPost = TestDataFactory.CreateTestPost(
        author.Id, audienceType: AudienceType.FriendsOnly);

    using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
    await context.Users.AddRangeAsync(author, friend, stranger);
    await context.Friendships.AddAsync(friendship);
    await context.Posts.AddAsync(friendsOnlyPost);
    await context.SaveChangesAsync();

    // Act
    var friendFeed = await postService.GetUserFeedPostsAsync(friend.Id, 0, 10);
    var strangerFeed = await postService.GetUserFeedPostsAsync(stranger.Id, 0, 10);

    // Assert
    friendFeed.Should().ContainSingle(p => p.Id == friendsOnlyPost.Id);
    strangerFeed.Should().BeEmpty();
}
```

## Testing Notifications

The notification system requires special attention due to its integration with PostService.

### Notification Creation Tests

```csharp
[Fact]
public async Task AddOrUpdateReactionAsync_NewReaction_CreatesNotification()
{
    // Arrange
    var author = TestDataFactory.CreateTestUser(id: "author1");
    var reactor = TestDataFactory.CreateTestUser(id: "reactor1");
    var post = TestDataFactory.CreateTestPost(authorId: "author1");

    using (var context = await dbFactory.CreateDbContextAsync())
    {
        context.Users.AddRange(author, reactor);
        context.Posts.Add(post);
        await context.SaveChangesAsync();

        // Act
        await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Love);

        // Assert
        var notifications = await notificationService
            .GetUserNotificationsAsync("author1", 10, 1);

        notifications.Should().HaveCount(1);
        notifications[0].Type.Should().Be(NotificationType.PostLiked);
        notifications[0].Message.Should().Contain("reacted to your post with love");
    }
}
```

### Smart Notification Logic Tests

```csharp
[Fact]
public async Task AddCommentAsync_OwnPost_DoesNotCreateNotification()
{
    // User comments on their own post - no notification
    await service.AddCommentAsync(post.Id, "user1", "My comment");

    var notifications = await notificationService
        .GetUserNotificationsAsync("user1", 10, 1);
    notifications.Should().BeEmpty();
}

[Fact]
public async Task AddOrUpdateReactionAsync_ChangeReaction_DoesNotCreateNewNotification()
{
    // Changing reaction type doesn't create new notification
    await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Like);
    var initialCount = (await notificationService.GetUserNotificationsAsync("author1", 10, 1)).Count;

    await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Love);
    var finalCount = (await notificationService.GetUserNotificationsAsync("author1", 10, 1)).Count;

    finalCount.Should().Be(initialCount);
}
```

## Best Practices

### 1. Test Naming Convention

Use descriptive names: `MethodName_Scenario_ExpectedBehavior`
- ✅ `CreatePostAsync_ValidPost_CreatesPostSuccessfully`
- ✅ `AddCommentAsync_ExceedsMaxDirectComments_ThrowsException`
- ❌ `TestPost` (unclear)

### 2. Arrange-Act-Assert Pattern

```csharp
[Fact]
public async Task TestMethod()
{
    // Arrange - Set up test data
    var user = TestDataFactory.CreateTestUser();

    // Act - Execute code under test
    var result = await service.DoSomething(user.Id);

    // Assert - Verify outcome
    result.Should().NotBeNull();
}
```

### 3. Use FluentAssertions

```csharp
// Instead of
Assert.Equal(5, list.Count);
Assert.True(result.IsSuccess);

// Use
list.Should().HaveCount(5);
result.IsSuccess.Should().BeTrue();
```

### 4. Test Isolation

Each test should be independent with its own database:

```csharp
[Fact]
public async Task Test1()
{
    var repoFactory = CreateTestRepositoryFactory("test1");
}

[Fact]
public async Task Test2()
{
    var repoFactory = CreateTestRepositoryFactory("test2");
}
```

### 5. Test One Thing at a Time

```csharp
// Bad - Testing multiple things
[Fact]
public async Task CreateAndDeletePost() { ... }

// Good - Separate tests
[Fact]
public async Task CreatePost_ValidData_CreatesSuccessfully() { ... }

[Fact]
public async Task DeletePost_ExistingPost_RemovesFromDatabase() { ... }
```

## Code Coverage

Target coverage:
- **80%+ overall**
- **90%+ for services**
- **70%+ for components**

Generate coverage reports:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## CI/CD Integration

Tests run automatically on:
- Push to `main`, `develop`, or feature branches
- Pull requests

See `.github/workflows/ci-cd.yml` for GitHub Actions configuration.
        };

        // Act
        var cut = RenderComponent<NotificationComponent>(parameters =>
            parameters.Add(p => p.Notification, notification));

        // Assert
        cut.Markup.Should().Contain("❤️"); // Heart emoji for Love reaction
        cut.Find(".notification-avatar img")
            .GetAttribute("src").Should().Be("/profile.jpg");
        cut.Find(".notification-unread-indicator").Should().NotBeNull();
    }

    [Fact]
    public void NotificationComponent_Clicked_InvokesCallback()
    {
        // Arrange
        var notification = CreateTestNotification();
        var clicked = false;

        var cut = RenderComponent<NotificationComponent>(parameters =>
            parameters
                .Add(p => p.Notification, notification)
                .Add(p => p.OnClick, () => clicked = true));

        // Act
        cut.Find(".notification-item").Click();

        // Assert
        clicked.Should().BeTrue();
    }
}
```

### Integration Testing Notifications

```csharp
[Fact]
public async Task UserInteraction_CreatesNotification_AppearsInUI()
{
    // This would be a full integration test with a real database

    // 1. User B reacts to User A's post
    // 2. Verify notification record in database
    // 3. Verify notification appears in User A's notification list
    // 4. Click notification
    // 5. Verify post modal opens
    // 6. Verify notification marked as read
    // 7. Verify unread count decreases
}
```

### Testing Notification Service

```csharp
public class NotificationServiceTests : TestBase
{
    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var userId = "user1";
        await CreateTestNotifications(userId, unreadCount: 5, readCount: 3);

        // Act
        var count = await NotificationService.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllNotificationsRead()
    {
        // Arrange
        var userId = "user1";
        await CreateTestNotifications(userId, unreadCount: 5);

        // Act
        var (success, _, updatedCount) = 
            await NotificationService.MarkAllAsReadAsync(userId);

        // Assert
        success.Should().BeTrue();
        updatedCount.Should().Be(5);

        var unreadCount = await NotificationService
            .GetUnreadCountAsync(userId);
        unreadCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteExpiredNotificationsAsync_DeletesOnlyExpired()
    {
        // Arrange
        await CreateNotificationWithExpiry(expiresAt: DateTime.UtcNow.AddDays(-1));
        await CreateNotificationWithExpiry(expiresAt: DateTime.UtcNow.AddDays(1));

        // Act
        var deletedCount = await NotificationService
            .DeleteExpiredNotificationsAsync();

        // Assert
        deletedCount.Should().Be(1);
    }
}
```

### Mock NotificationService for PostService Tests

When testing PostService, you need to provide a mock NotificationService:

```csharp
private static NotificationService CreateMockNotificationService()
{
    var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
    var logger = new Mock<ILogger<NotificationService>>();
    return new NotificationService(dbFactory.Object, logger.Object);
}

// Usage in test
var service = new PostService(
    dbFactory, 
    logger, 
    CreateTestSiteSettings(), 
    CreateMockWebHostEnvironment(),
    CreateMockNotificationService()  // ← Add this parameter
);
```

## Troubleshooting

### Tests failing locally but passing in CI
- Check .NET SDK version matches CI
- Ensure database is properly isolated
- Verify file paths are relative

### bUnit tests can't find elements
- Check component renders correctly
- Verify CSS selectors match rendered HTML
- Use `cut.MarkupMatches()` to debug HTML structure

### In-memory database issues
- Use unique database names per test
- Ensure proper disposal of contexts
- Check entity configurations in OnModelCreating

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [bUnit Documentation](https://bunit.dev/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Moq Documentation](https://github.com/moq/moq4)
