# FreeSpeak Testing Guide

## Overview
This project uses a comprehensive testing strategy with **xUnit.net** for business logic and **bUnit** for Blazor component testing. All tests follow the **AAA (Arrange, Act, Assert)** pattern for clarity and maintainability.

## Test Structure

```
FreeSpeakWeb.Tests/
├── Infrastructure/
│   ├── TestBase.cs              # Base class with common test utilities
│   └── TestDataFactory.cs       # Factory for creating test entities
├── Services/
│   ├── FriendsServiceTests.cs   # Tests for friend management
│   ├── PostServiceTests.cs      # Tests for post operations
│   ├── PostServiceEdgeCaseTests.cs # Edge cases and error handling
│   ├── NotificationServiceTests.cs # Tests for notification system
│   └── ProfilePictureServiceTests.cs
└── Components/
    ├── FeedArticleTests.cs      # bUnit tests for feed components
    ├── MultiLineCommentDisplayTests.cs
    └── NotificationComponentTests.cs # Tests for notification display
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

## Writing New Tests

### Service Tests (xUnit)

```csharp
public class MyServiceTests : TestBase
{
    [Fact]
    public async Task MethodName_Scenario_ExpectedBehavior()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("TestDb");
        var logger = CreateMockLogger<MyService>();
        var service = new MyService(dbFactory, logger);
        
        var testData = TestDataFactory.CreateTestUser();
        
        // Act
        var result = await service.DoSomethingAsync(testData.Id);
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
}
```

### Component Tests (bUnit)

```csharp
public class MyComponentTests : TestContext
{
    [Fact]
    public void MyComponent_WhenRendered_ShouldDisplayContent()
    {
        // Arrange & Act
        var cut = RenderComponent<MyComponent>(parameters => parameters
            .Add(p => p.Title, "Test Title")
            .Add(p => p.Content, "Test Content"));
        
        // Assert
        cut.Find("h1").TextContent.Should().Be("Test Title");
        cut.Markup.Should().Contain("Test Content");
    }
}
```

## Test Infrastructure

### TestBase
Provides common utilities:
- `CreateInMemoryContext()` - Creates isolated in-memory database
- `CreateDbContextFactory()` - Creates mock DbContext factory
- `CreateMockLogger<T>()` - Creates mock logger

### TestDataFactory
Factory methods for creating test entities:
- `CreateTestUser()`
- `CreateTestPost()`
- `CreateTestComment()`
- `CreateTestLike()`
- `CreateTestFriendship()`

## CI/CD Integration

### GitHub Actions
Tests run automatically on:
- Push to `main`, `develop`, or feature branches
- Pull requests to `main` or `develop`

See `.github/workflows/ci-cd.yml` for configuration.

### Azure DevOps
Tests run automatically on:
- Build pipeline triggers
- Pull request validation

See `azure-pipelines.yml` for configuration.

## Code Coverage

Aim for:
- **80%+ overall coverage**
- **90%+ coverage for services**
- **70%+ coverage for components**

View coverage reports in:
- GitHub Actions: Test Results tab
- Azure DevOps: Code Coverage tab
- Locally: Generated HTML reports in `coverage/` folder

## Best Practices

1. **Follow AAA Pattern**: Arrange, Act, Assert
2. **One assertion per test** when possible
3. **Descriptive test names**: `MethodName_Scenario_ExpectedBehavior`
4. **Use FluentAssertions** for readable assertions
5. **Isolated tests**: Each test should be independent
6. **Test edge cases**: Null values, empty strings, boundary conditions
7. **Mock external dependencies**: Use Moq for interfaces
8. **Clean up resources**: Dispose contexts and streams

## Example Test Scenarios

### ✅ Good Test
```csharp
[Fact]
public async Task CreatePostAsync_WithValidContent_ShouldCreatePost()
{
    // Clear arrange, act, assert sections
    // Tests one specific scenario
    // Descriptive name explains what's being tested
}
```

### ❌ Bad Test
```csharp
[Fact]
public async Task TestPost()
{
    // Unclear what's being tested
    // No clear AAA structure
    // Tests multiple things at once
}
```

## Testing Notifications

The notification system requires special attention due to its integration with PostService. Here are key scenarios to test:

### Notification Creation Tests

```csharp
[Fact]
public async Task AddOrUpdateReactionAsync_NewReaction_CreatesNotification()
{
    // Arrange
    var dbFactory = CreateDbContextFactory("NotificationTest");
    var logger = CreateMockLogger<PostService>();
    var notificationService = CreateMockNotificationService();
    var service = new PostService(dbFactory, logger, 
        CreateTestSiteSettings(), CreateMockWebHostEnvironment(), 
        notificationService);

    var author = TestDataFactory.CreateTestUser(id: "author1");
    var reactor = TestDataFactory.CreateTestUser(id: "reactor1");

    using (var context = await dbFactory.CreateDbContextAsync())
    {
        context.Users.AddRange(author, reactor);
        var post = TestDataFactory.CreateTestPost(authorId: "author1");
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
        notifications[0].IsRead.Should().BeFalse();
    }
}
```

### Smart Notification Logic Tests

```csharp
[Fact]
public async Task AddCommentAsync_OwnPost_DoesNotCreateNotification()
{
    // Arrange
    var author = TestDataFactory.CreateTestUser(id: "user1");
    var post = TestDataFactory.CreateTestPost(authorId: "user1");

    // Act - User comments on their own post
    await service.AddCommentAsync(post.Id, "user1", "My comment");

    // Assert - No notification should be created
    var notifications = await notificationService
        .GetUserNotificationsAsync("user1", 10, 1);

    notifications.Should().BeEmpty();
}

[Fact]
public async Task AddOrUpdateReactionAsync_ChangeReaction_DoesNotCreateNewNotification()
{
    // Arrange - User already has a Like reaction
    await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Like);
    var initialNotifications = await notificationService
        .GetUserNotificationsAsync("author1", 10, 1);

    // Act - User changes to Love reaction
    await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Love);

    // Assert - No new notification created
    var finalNotifications = await notificationService
        .GetUserNotificationsAsync("author1", 10, 1);

    finalNotifications.Count.Should().Be(initialNotifications.Count);
}
```

### Notification Data Tests

```csharp
[Fact]
public async Task PostReactionNotification_ContainsCorrectData()
{
    // Arrange & Act
    await service.AddOrUpdateReactionAsync(post.Id, "reactor1", LikeType.Love);

    // Assert
    var notifications = await notificationService
        .GetUserNotificationsAsync("author1", 10, 1);

    var notification = notifications.First();
    var data = JsonSerializer.Deserialize<NotificationData>(notification.Data);

    data.PostId.Should().Be(post.Id);
    data.ReactorId.Should().Be("reactor1");
    data.ReactorName.Should().Be(reactor.UserName);
    data.ReactorProfilePicture.Should().Be(reactor.ProfilePictureUrl);
    data.ReactionType.Should().Be("Love");
}
```

### Component Testing for Notifications

```csharp
public class NotificationComponentTests : TestContext
{
    [Fact]
    public void NotificationComponent_PostLikedWithLove_ShowsHeartEmoji()
    {
        // Arrange
        var notification = new UserNotification
        {
            Id = 1,
            Type = NotificationType.PostLiked,
            Message = "john_doe reacted to your post with love",
            Data = JsonSerializer.Serialize(new {
                PostId = 123,
                ReactorName = "john_doe",
                ReactorProfilePicture = "/profile.jpg",
                ReactionType = "Love"
            }),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
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
