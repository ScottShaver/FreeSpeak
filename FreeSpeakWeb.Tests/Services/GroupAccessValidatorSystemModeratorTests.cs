using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for the system moderator override in <see cref="GroupAccessValidator.IsGroupAdminOrModeratorAsync"/>.
    /// </summary>
    public class GroupAccessValidatorSystemModeratorTests : TestBase
    {
        private static GroupAccessValidator CreateValidator(Microsoft.EntityFrameworkCore.IDbContextFactory<ApplicationDbContext> dbFactory, Mock<IRoleService> roles)
            => new(dbFactory, new Mock<ILogger<GroupAccessValidator>>().Object, roles.Object);

        private static async Task<int> SeedGroupAsync(Microsoft.EntityFrameworkCore.IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            var creator = TestDataFactory.CreateTestUser(id: "creator1");
            var member = TestDataFactory.CreateTestUser(id: "member1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            using var context = await dbFactory.CreateDbContextAsync();
            context.Users.AddRange(creator, member);
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            context.GroupUsers.Add(new GroupUser { GroupId = group.Id, UserId = "member1" });
            await context.SaveChangesAsync();
            return group.Id;
        }

        /// <summary>A system moderator who is not a member counts as a group moderator.</summary>
        [Fact]
        public async Task IsGroupAdminOrModerator_SystemModerator_IsTrue()
        {
            var dbFactory = CreateDbContextFactory("SysModOverride1");
            var roles = new Mock<IRoleService>();
            roles.Setup(r => r.IsUserInRoleAsync("sysmod", "SystemModerator")).ReturnsAsync(true);
            var groupId = await SeedGroupAsync(dbFactory);

            (await CreateValidator(dbFactory, roles).IsGroupAdminOrModeratorAsync(groupId, "sysmod")).Should().BeTrue();
        }

        /// <summary>A system administrator keeps full access.</summary>
        [Fact]
        public async Task IsGroupAdminOrModerator_SystemAdministrator_IsTrue()
        {
            var dbFactory = CreateDbContextFactory("SysModOverride2");
            var roles = new Mock<IRoleService>();
            roles.Setup(r => r.IsSystemAdministratorAsync("sysadmin")).ReturnsAsync(true);
            var groupId = await SeedGroupAsync(dbFactory);

            (await CreateValidator(dbFactory, roles).IsGroupAdminOrModeratorAsync(groupId, "sysadmin")).Should().BeTrue();
        }

        /// <summary>Plain members and non-members without a system role are still denied.</summary>
        [Theory]
        [InlineData("member1")]
        [InlineData("stranger")]
        public async Task IsGroupAdminOrModerator_PlainUser_IsFalse(string userId)
        {
            var dbFactory = CreateDbContextFactory("SysModOverride3_" + userId);
            var roles = new Mock<IRoleService>();
            var groupId = await SeedGroupAsync(dbFactory);

            (await CreateValidator(dbFactory, roles).IsGroupAdminOrModeratorAsync(groupId, userId)).Should().BeFalse();
        }
    }
}
