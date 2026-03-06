using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FreeSpeakWeb.Tests.Infrastructure
{
    /// <summary>
    /// Base class for tests that provides common test infrastructure
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected ApplicationDbContext CreateInMemoryContext(string databaseName = "")
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                databaseName = Guid.NewGuid().ToString();
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            return new ApplicationDbContext(options);
        }

        protected IDbContextFactory<ApplicationDbContext> CreateDbContextFactory(string databaseName = "")
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                databaseName = Guid.NewGuid().ToString();
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            factory.Setup(f => f.CreateDbContext())
                .Returns(() => new ApplicationDbContext(options));
            factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ApplicationDbContext(options));

            return factory.Object;
        }

        protected ILogger<T> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>().Object;
        }

        public void Dispose()
        {
            // Cleanup if needed
            GC.SuppressFinalize(this);
        }
    }
}
