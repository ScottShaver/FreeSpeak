using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="GroupAccessValidator.ValidateUserCanViewGroupPageAsync"/>, which guards the group home page.
    /// </summary>
    public class GroupAccessValidatorPageViewTests : TestBase
    {
        private static GroupAccessValidator CreateValidator(IDbContextFactory<ApplicationDbContext> dbFactory, bool isSystemAdmin = false)
        {
            var roleService = new Mock<IRoleService>();
            roleService.Setup(r => r.IsSystemAdministratorAsync(It.IsAny<string>())).ReturnsAsync(isSystemAdmin);
            return new GroupAccessValidator(dbFactory, new Mock<ILogger<GroupAccessValidator>>().Object, roleService.Object);
        }

        private static async Task<int> SeedGroupAsync(IDbContextFactory<ApplicationDbContext> dbFactory, bool isPublic, bool banUser)
        {
            var creator = TestDataFactory.CreateTestUser(id: "creator1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.IsPublic = isPublic;

            using var context = await dbFactory.CreateDbContextAsync();
            context.Users.AddRange(creator, user);
            context.Groups.Add(group);
            await context.SaveChangesAsync();

            if (banUser)
            {
                var ban = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user1");
                ban.GroupId = group.Id;
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            return group.Id;
        }

        [Fact]
        public async Task NonMember_PublicGroup_CanView()
        {
            var dbFactory = CreateDbContextFactory("PageViewNonMemberPublic");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: true, banUser: false);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewGroupPageAsync(groupId, true, "user1");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task NonMember_PrivateGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("PageViewNonMemberPrivate");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: false);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewGroupPageAsync(groupId, false, "user1");

            success.Should().BeFalse();
        }

        [Fact]
        public async Task BannedUser_PublicGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("PageViewBannedPublic");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: true, banUser: true);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewGroupPageAsync(groupId, true, "user1");

            success.Should().BeFalse();
        }

        [Fact]
        public async Task SystemAdministrator_NonMember_CanViewPrivateGroup()
        {
            var dbFactory = CreateDbContextFactory("PageViewSysAdminPrivate");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: false);

            var (success, _) = await CreateValidator(dbFactory, isSystemAdmin: true).ValidateUserCanViewGroupPageAsync(groupId, false, "user1");

            success.Should().BeTrue();
        }
    }
}
