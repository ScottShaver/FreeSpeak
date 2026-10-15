using FluentAssertions;
using FreeSpeakWeb.Data;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for the safety guards of <see cref="AgentTestSeeder"/>.
    /// </summary>
    public class AgentTestSeederTests
    {
        /// <summary>Seeding is allowed when every guard passes.</summary>
        [Fact]
        public void EnsureSafeToRun_AllChecksPass_DoesNotThrow()
        {
            var act = () => AgentTestSeeder.EnsureSafeToRun(true, true, "FreeSpeak_AgentTest");
            act.Should().NotThrow();
        }

        /// <summary>Seeding is refused outside Development.</summary>
        [Fact]
        public void EnsureSafeToRun_NotDevelopment_Throws()
        {
            var act = () => AgentTestSeeder.EnsureSafeToRun(false, true, "FreeSpeak_AgentTest");
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>Seeding is refused unless explicitly enabled.</summary>
        [Fact]
        public void EnsureSafeToRun_NotEnabled_Throws()
        {
            var act = () => AgentTestSeeder.EnsureSafeToRun(true, false, "FreeSpeak_AgentTest");
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>Seeding is refused for databases without the required suffix, or without a name.</summary>
        [Theory]
        [InlineData("FreeSpeak")]
        [InlineData("FreeSpeak_AgentTest_snapshot")]
        [InlineData("freespeak_agenttest")]
        [InlineData("")]
        [InlineData(null)]
        public void EnsureSafeToRun_BadDatabaseName_Throws(string? databaseName)
        {
            var act = () => AgentTestSeeder.EnsureSafeToRun(true, true, databaseName);
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
