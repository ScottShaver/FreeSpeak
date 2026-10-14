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
    /// Tests for <see cref="GroupAccessValidator.ValidateUserCanViewAsync"/>, which guards read access
    /// to group content such as the single group post route.
    /// </summary>
    public class GroupAccessValidatorViewTests : TestBase
    {
        private static GroupAccessValidator CreateValidator(Microsoft.EntityFrameworkCore.IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            var logger = new Mock<ILogger<GroupAccessValidator>>();
            var roleService = new Mock<IRoleService>();
            return new GroupAccessValidator(dbFactory, logger.Object, roleService.Object);
        }

        private static async Task<int> SeedGroupAsync(
            Microsoft.EntityFrameworkCore.IDbContextFactory<ApplicationDbContext> dbFactory,
            bool isPublic,
            bool banUser,
            bool memberUser)
        {
            var creator = TestDataFactory.CreateTestUser(id: "creator1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.IsPublic = isPublic;

            using var context = await dbFactory.CreateDbContextAsync();
            context.Users.AddRange(creator, user);
            context.Groups.Add(group);
            await context.SaveChangesAsync();

            if (memberUser)
            {
                context.GroupUsers.Add(new GroupUser { GroupId = group.Id, UserId = "user1" });
            }

            if (banUser)
            {
                var ban = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user1");
                ban.GroupId = group.Id;
                context.GroupBannedMembers.Add(ban);
            }

            await context.SaveChangesAsync();
            return group.Id;
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_BannedUserPublicGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("ViewBannedPublic");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: true, banUser: true, memberUser: false);

            var (success, error) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, true, "user1");

            success.Should().BeFalse();
            error.Should().Contain("banned");
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_BannedUserPrivateGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("ViewBannedPrivate");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: true, memberUser: true);

            var (success, error) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, false, "user1");

            success.Should().BeFalse();
            error.Should().Contain("banned");
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_NonBannedUserPublicGroup_IsAllowed()
        {
            var dbFactory = CreateDbContextFactory("ViewPublicOk");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: true, banUser: false, memberUser: false);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, true, "user1");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_AnonymousPublicGroup_IsAllowed()
        {
            var dbFactory = CreateDbContextFactory("ViewAnonPublic");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: true, banUser: false, memberUser: false);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, true, null);

            success.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_AnonymousPrivateGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("ViewAnonPrivate");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: false, memberUser: false);

            var (success, error) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, false, null);

            success.Should().BeFalse();
            error.Should().Contain("logged in");
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_NonMemberPrivateGroup_IsDenied()
        {
            var dbFactory = CreateDbContextFactory("ViewPrivateNonMember");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: false, memberUser: false);

            var (success, error) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, false, "user1");

            success.Should().BeFalse();
            error.Should().Contain("must be a member");
        }

        [Fact]
        public async Task ValidateUserCanViewAsync_MemberPrivateGroup_IsAllowed()
        {
            var dbFactory = CreateDbContextFactory("ViewPrivateMember");
            var groupId = await SeedGroupAsync(dbFactory, isPublic: false, banUser: false, memberUser: true);

            var (success, _) = await CreateValidator(dbFactory).ValidateUserCanViewAsync(groupId, false, "user1");

            success.Should().BeTrue();
        }
    }
}
