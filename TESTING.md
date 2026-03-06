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
│   └── ProfilePictureServiceTests.cs
└── Components/
    ├── FeedArticleTests.cs      # bUnit tests for feed components
    └── MultiLineCommentDisplayTests.cs
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
