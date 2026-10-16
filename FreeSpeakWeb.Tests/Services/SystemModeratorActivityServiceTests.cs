using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="SystemModeratorActivityService"/>.
    /// </summary>
    public class SystemModeratorActivityServiceTests
    {
        private const string Moderator = "mod";
        private const string Admin = "admin";
        private const string Plain = "plain";

        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<IGroupReportModerationRepository> _users = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly ModerationSettings _settings = new();
        private readonly SystemModeratorActivityService _service;

        /// <summary>
        /// Creates the service with mocked dependencies and a moderator and an administrator.
        /// </summary>
        public SystemModeratorActivityServiceTests()
        {
            _roles.Setup(r => r.IsUserInRoleAsync(Moderator, "SystemModerator")).ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Admin, "SystemAdministrator")).ReturnsAsync(true);
            _users.Setup(u => u.GetUserDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => "Name " + id);
            _service = new SystemModeratorActivityService(_audit.Object, _users.Object, _roles.Object, Options.Create(_settings), NullLogger<SystemModeratorActivityService>.Instance);
        }

        private static AuditLog Row(long id, DateTime at, string user = Moderator, ActionCategory category = ActionCategory.SystemModeratorRemovePost, string? detail = null)
            => new()
            {
                Id = id,
                UserId = user,
                ActionStamp = at,
                ActionCategory = category.ToString(),
                ActionDetails = detail ?? AuditLogDetailsSerializer.ToJson(new SystemModeratorRemovePostDetails
                {
                    PostAuthorId = "author",
                    PostAuthorDisplayName = "Author A",
                    GroupId = 2,
                    GroupName = "Beta",
                    ViolationReason = "Spam",
                    ContentSummary = "=cmd|' /C calc'!A0"
                })
            };

        private void Rows(List<AuditLog> rows, int total)
            => _audit.Setup(a => a.GetByCategoriesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((rows, total));

        /// <summary>Only system moderators and administrators may read activity; others never reach the repository.</summary>
        [Theory]
        [InlineData(Moderator, true)]
        [InlineData(Admin, true)]
        [InlineData(Plain, false)]
        public async Task GetActivityAsync_RoleGate(string user, bool allowed)
        {
            Rows(new List<AuditLog> { Row(1, new DateTime(2026, 6, 9, 14, 5, 7)) }, 1);

            var page = await _service.GetActivityAsync(user, new ModeratorActivityFilter(), 1, 25);

            page.IsAuthorized.Should().Be(allowed);
            page.Items.Should().HaveCount(allowed ? 1 : 0);
            _audit.Verify(a => a.GetByCategoriesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()), allowed ? Times.Once : Times.Never);
        }

        /// <summary>Entries carry UTC timestamps, names and the group read from the details.</summary>
        [Fact]
        public async Task GetActivityAsync_MapsEntries()
        {
            Rows(new List<AuditLog> { Row(1, new DateTime(2026, 6, 9, 14, 5, 7, DateTimeKind.Unspecified)) }, 1);

            var page = await _service.GetActivityAsync(Moderator, new ModeratorActivityFilter(), 1, 25);

            var e = page.Items.Single();
            e.TimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
            e.ModeratorName.Should().Be("Name mod");
            e.GroupName.Should().Be("Beta");
            e.GroupId.Should().Be(2);
            e.TargetUserName.Should().Be("Author A");
            e.Action.Should().Be(ActionCategory.SystemModeratorRemovePost);
        }

        /// <summary>Filters map to repository arguments: single category, moderator, inclusive UTC day bounds and paging.</summary>
        [Fact]
        public async Task GetActivityAsync_PassesFiltersAndPaging()
        {
            Rows(new List<AuditLog>(), 0);
            var filter = new ModeratorActivityFilter("m2", ActionCategory.SystemModeratorWarnUser, new DateTime(2026, 6, 1), new DateTime(2026, 6, 3));

            await _service.GetActivityAsync(Moderator, filter, 3, 10);

            _audit.Verify(a => a.GetByCategoriesAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.Single() == nameof(ActionCategory.SystemModeratorWarnUser)),
                "m2",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc),
                20, 10), Times.Once);
        }

        /// <summary>An action outside the moderator activity set falls back to all activity categories.</summary>
        [Fact]
        public async Task GetActivityAsync_UnknownCategory_UsesAllCategories()
        {
            Rows(new List<AuditLog>(), 0);

            await _service.GetActivityAsync(Moderator, new ModeratorActivityFilter(null, ActionCategory.SystemModeratorActivityExport), 1, 25);

            _audit.Verify(a => a.GetByCategoriesAsync(It.Is<IReadOnlyCollection<string>>(c => c.Count == SystemModeratorActivityService.ActivityCategories.Length), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), 0, 25), Times.Once);
        }

        /// <summary>CSV cells starting with formula characters are neutralized and times are UTC.</summary>
        [Fact]
        public void BuildCsv_NeutralizesFormulasAndUsesUtc()
        {
            var entry = new ModeratorActivityEntry(1, new DateTime(2026, 6, 9, 14, 5, 7, DateTimeKind.Utc), "m", "=HYPERLINK(\"x\")", ActionCategory.SystemModeratorRemovePost, "a", "A, B", "+1", 2, "Beta");

            var csv = SystemModeratorActivityService.BuildCsv(new[] { entry });

            csv.Should().Contain("2026-06-09 14:05:07Z");
            csv.Should().NotContain(",=HYPERLINK");
            csv.Should().Contain("\"A, B\"");
        }

        /// <summary>Export is refused for non-moderators and audited for moderators.</summary>
        [Fact]
        public async Task ExportCsvAsync_AuthorizesAndAudits()
        {
            Rows(new List<AuditLog> { Row(1, new DateTime(2026, 6, 9)) }, 1);

            (await _service.ExportCsvAsync(Plain, new ModeratorActivityFilter())).IsAuthorized.Should().BeFalse();
            _audit.Verify(a => a.LogActionAsync(It.IsAny<string>(), ActionCategory.SystemModeratorActivityExport, It.IsAny<SystemModeratorActivityExportDetails>()), Times.Never);

            var result = await _service.ExportCsvAsync(Moderator, new ModeratorActivityFilter());

            result.IsAuthorized.Should().BeTrue();
            result.RowCount.Should().Be(1);
            result.Truncated.Should().BeFalse();
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorActivityExport, It.Is<SystemModeratorActivityExportDetails>(d => d.RowCount == 1 && !d.Truncated)), Times.Once);
        }

        /// <summary>Exports beyond the configured cap are truncated and flagged.</summary>
        [Fact]
        public async Task ExportCsvAsync_AboveCap_IsTruncated()
        {
            _settings.ActivityExportMaxRows = 2;
            Rows(Enumerable.Range(1, 3).Select(i => Row(i, new DateTime(2026, 6, 9))).ToList(), 3);

            var result = await _service.ExportCsvAsync(Admin, new ModeratorActivityFilter());

            result.RowCount.Should().Be(2);
            result.Truncated.Should().BeTrue();
        }

        /// <summary>Moderator options are limited to authorized users and sorted by name.</summary>
        [Fact]
        public async Task GetModeratorOptionsAsync_SortedAndGated()
        {
            _audit.Setup(a => a.GetDistinctUserIdsByCategoriesAsync(It.IsAny<IReadOnlyCollection<string>>())).ReturnsAsync(new List<string> { "z", "b" });

            (await _service.GetModeratorOptionsAsync(Plain)).Should().BeEmpty();
            var options = await _service.GetModeratorOptionsAsync(Moderator);

            options.Select(o => o.UserId).Should().Equal("b", "z");
        }
    }
}
