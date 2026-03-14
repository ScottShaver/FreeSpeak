using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Moq;

namespace FreeSpeakWeb.IntegrationTests.Infrastructure;

/// <summary>
/// Provides factory methods for creating mock repository instances for integration testing.
/// </summary>
public static class MockRepositories
{
    /// <summary>
    /// Creates a mock IAuditLogRepository that returns Task.CompletedTask for LogActionAsync calls.
    /// </summary>
    /// <returns>A configured Mock of IAuditLogRepository.</returns>
    public static Mock<IAuditLogRepository> CreateMockAuditLogRepository()
    {
        var mock = new Mock<IAuditLogRepository>();
        mock.Setup(r => r.LogActionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return mock;
    }
}
