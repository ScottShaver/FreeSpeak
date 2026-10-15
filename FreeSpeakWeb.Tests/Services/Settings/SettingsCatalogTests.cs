using FluentAssertions;
using FreeSpeakWeb.Services.Settings;
using Xunit;

namespace FreeSpeakWeb.Tests.Services.Settings
{
    /// <summary>
    /// Verifies the settings catalog matches the keys, secrets and restart metadata required by the
    /// system settings administration spec.
    /// </summary>
    public class SettingsCatalogTests
    {
        /// <summary>Verifies every key listed in the spec's setting group table is editable.</summary>
        [Theory]
        [InlineData("site", "SiteName,MaxFeedPostCommentDepth,MaxFeedPostDirectCommentCount,AllowOpenGroupCreation,AllowPostAttachments")]
        [InlineData("smtp", "Smtp:Server,Smtp:Port,Smtp:SenderName,Smtp:SenderEmail,Smtp:AuthenticationRequired,Smtp:Account,Smtp:Password,Smtp:UseSsl,UseIdentityNoOpEmailSender")]
        [InlineData("virusscan", "VirusScan:Enabled,VirusScan:ClamAvHost,VirusScan:ClamAvPort,VirusScan:MaxStreamSizeBytes,VirusScan:FailOpen,VirusScan:TimeoutSeconds")]
        [InlineData("profiling", "Profiling:Enabled,Profiling:ShowControls,Profiling:MaxResults,Profiling:ProfileDatabase,Profiling:AllowOutsideDevelopment")]
        [InlineData("caching", "Caching:UseRedis,Caching:RedisConnectionString")]
        public void Group_ContainsAllSpecKeys(string group, string keys)
        {
            var actual = SettingsCatalog.Find(group)!.Fields.Select(f => f.Key);

            actual.Should().Contain(keys.Split(','));
        }

        /// <summary>Verifies each rate limit policy exposes MaxRequests, WindowMinutes and QueueLimit.</summary>
        [Fact]
        public void RateLimiting_ContainsAllPolicyKeys()
        {
            var keys = SettingsCatalog.Find("ratelimiting")!.Fields.Select(f => f.Key).ToList();

            keys.Should().Contain("RateLimiting:UseDistributed");
            foreach (var policy in new[] { "Global", "FileDownload", "Login", "Api" })
            {
                keys.Should().Contain(new[]
                {
                    $"RateLimiting:{policy}:MaxRequests",
                    $"RateLimiting:{policy}:WindowMinutes",
                    $"RateLimiting:{policy}:QueueLimit"
                });
            }
        }

        /// <summary>Verifies Logging is restricted to the fixed category list.</summary>
        [Fact]
        public void Logging_ContainsExactlyTheFixedCategories()
        {
            var keys = SettingsCatalog.Find("logging")!.Fields.Select(f => f.Key);

            keys.Should().BeEquivalentTo(new[]
            {
                "Logging:LogLevel:Default", "Logging:LogLevel:Microsoft", "Logging:LogLevel:Microsoft.AspNetCore",
                "Logging:LogLevel:Microsoft.Hosting.Lifetime", "Logging:LogLevel:Microsoft.EntityFrameworkCore",
                "Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command", "Logging:LogLevel:System"
            });
        }

        /// <summary>Verifies only restart-bound groups are flagged and the live groups are not.</summary>
        [Theory]
        [InlineData("site", false)]
        [InlineData("smtp", false)]
        [InlineData("virusscan", false)]
        [InlineData("logging", false)]
        [InlineData("ratelimiting", true)]
        [InlineData("profiling", true)]
        [InlineData("caching", true)]
        public void RestartRequired_MatchesSpec(string group, bool expected)
        {
            SettingsCatalog.Find(group)!.RestartRequired.Should().Be(expected);
        }

        /// <summary>Verifies the Smtp password and Redis connection string are the secret fields.</summary>
        [Fact]
        public void SecretFields_AreSmtpPasswordAndRedisConnectionString()
        {
            var secrets = SettingsCatalog.Groups.SelectMany(g => g.Fields).Where(f => f.IsSecret).Select(f => f.Key);

            secrets.Should().BeEquivalentTo(new[] { "Smtp:Password", "Caching:RedisConnectionString" });
        }

        /// <summary>Verifies bootstrap settings are not editable.</summary>
        [Fact]
        public void NonEditableBootstrapSettings_AreNotInCatalog()
        {
            var keys = SettingsCatalog.Groups.SelectMany(g => g.Fields).Select(f => f.Key).ToList();

            keys.Should().NotContain(k => k.StartsWith("ConnectionStrings") || k.StartsWith("SystemAdministratorInitInfo") || k == "AllowedHosts");
        }
    }
}
