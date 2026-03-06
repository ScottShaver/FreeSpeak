using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Base class for integration tests using TestContainers PostgreSQL
    /// </summary>
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        private PostgreSqlContainer? _postgresContainer;
        protected string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            // Create and start PostgreSQL container
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("freespeaktest")
                .WithUsername("testuser")
                .WithPassword("testpass")
                .WithCleanUp(true)
                .Build();

            await _postgresContainer.StartAsync();
            ConnectionString = _postgresContainer.GetConnectionString();

            // Run migrations
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_postgresContainer != null)
            {
                await _postgresContainer.DisposeAsync();
            }
        }

        protected ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            return new ApplicationDbContext(options);
        }

        protected IDbContextFactory<ApplicationDbContext> CreateDbContextFactory()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            return new TestDbContextFactory(options);
        }

        protected ILogger<T> CreateLogger<T>()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            return loggerFactory.CreateLogger<T>();
        }

        private class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly DbContextOptions<ApplicationDbContext> _options;

            public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
            {
                _options = options;
            }

            public ApplicationDbContext CreateDbContext()
            {
                return new ApplicationDbContext(_options);
            }

            public async Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            {
                return await Task.FromResult(new ApplicationDbContext(_options));
            }
        }
    }
}
