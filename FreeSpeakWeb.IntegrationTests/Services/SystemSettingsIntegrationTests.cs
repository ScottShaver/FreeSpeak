using FluentAssertions;
using FreeSpeakWeb.Configuration;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for the SystemSettings table, repository and configuration provider.
    /// </summary>
    public class SystemSettingsIntegrationTests : IntegrationTestBase
    {
        private SystemSettingsRepository CreateRepository() =>
            new(CreateDbContextFactory(), NullLogger<SystemSettingsRepository>.Instance);

        /// <summary>Verifies the migration creates the table and rows round-trip.</summary>
        [Fact]
        public async Task Migration_CreatesTable_AndRowsRoundTrip()
        {
            var repo = CreateRepository();

            await repo.UpsertManyAsync(new[] { new SystemSetting { Key = "Smtp:Server", Value = "mail", UpdatedAtUtc = DateTime.UtcNow } });
            await repo.UpsertManyAsync(new[] { new SystemSetting { Key = "Smtp:Server", Value = "mail2", UpdatedAtUtc = DateTime.UtcNow } });

            var rows = await repo.GetAllAsync();
            rows.Should().ContainSingle(r => r.Key == "Smtp:Server" && r.Value == "mail2");
            (await repo.DeleteManyAsync(new[] { "Smtp:Server" })).Should().Be(1);
            (await repo.GetAllAsync()).Should().BeEmpty();
        }

        /// <summary>Verifies the provider reads rows over file values and reloads on request.</summary>
        [Fact]
        public async Task Provider_ReadsAndReloads()
        {
            var repo = CreateRepository();
            var source = new DatabaseConfigurationSource
            {
                RowReader = new NpgsqlSystemSettingRowReader(ConnectionString),
                PollInterval = Timeout.InfiniteTimeSpan,
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["VirusScan:ClamAvHost"] = "file" })
                .Add(source)
                .Build();
            config["VirusScan:ClamAvHost"].Should().Be("file");

            await repo.UpsertManyAsync(new[] { new SystemSetting { Key = "VirusScan:ClamAvHost", Value = "db", UpdatedAtUtc = DateTime.UtcNow } });
            source.Reload();

            config["VirusScan:ClamAvHost"].Should().Be("db");
        }

        /// <summary>Verifies a second instance picks up changes through polling.</summary>
        [Fact]
        public async Task SecondInstance_PicksUpChanges_ByPolling()
        {
            var repo = CreateRepository();
            var source = new DatabaseConfigurationSource
            {
                RowReader = new NpgsqlSystemSettingRowReader(ConnectionString),
                PollInterval = TimeSpan.FromMilliseconds(200),
            };
            var config = new ConfigurationBuilder().Add(source).Build();

            await repo.UpsertManyAsync(new[] { new SystemSetting { Key = "Smtp:Port", Value = "2525", UpdatedAtUtc = DateTime.UtcNow } });

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (config["Smtp:Port"] != "2525" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            config["Smtp:Port"].Should().Be("2525");
            source.Provider!.Dispose();
        }

        /// <summary>Verifies the provider tolerates the table being absent.</summary>
        [Fact]
        public async Task Provider_ToleratesMissingTable()
        {
            await using (var ctx = CreateDbContext())
            {
                await ctx.Database.ExecuteSqlRawAsync("DROP TABLE \"SystemSettings\"");
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["A"] = "1" })
                .Add(new DatabaseConfigurationSource { RowReader = new NpgsqlSystemSettingRowReader(ConnectionString), PollInterval = Timeout.InfiniteTimeSpan })
                .Build();

            config["A"].Should().Be("1");
        }
    }
}
