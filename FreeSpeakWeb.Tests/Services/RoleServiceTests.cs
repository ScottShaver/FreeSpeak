using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="RoleService"/> role changes and the last-system-administrator guard.
    /// </summary>
    public class RoleServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<RoleManager<IdentityRole>> _roleManager;

        public RoleServiceTests()
        {
            _userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            _roleManager = new Mock<RoleManager<IdentityRole>>(
                Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);
        }

        private RoleService CreateService() =>
            new(_userManager.Object, _roleManager.Object, Mock.Of<ILogger<RoleService>>());

        private ApplicationUser SetupUser(string id, IList<ApplicationUser> admins, bool isAdmin)
        {
            var user = new ApplicationUser { Id = id, UserName = id };
            _userManager.Setup(m => m.FindByIdAsync(id)).ReturnsAsync(user);
            _userManager.Setup(m => m.IsInRoleAsync(user, "SystemAdministrator")).ReturnsAsync(isAdmin);
            _userManager.Setup(m => m.RemoveFromRoleAsync(user, "SystemAdministrator")).ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.GetUsersInRoleAsync("SystemAdministrator")).ReturnsAsync(admins);
            return user;
        }

        [Fact]
        public async Task RemoveUserFromRole_LastSystemAdministrator_IsRefused()
        {
            var admins = new List<ApplicationUser>();
            var user = SetupUser("admin1", admins, true);
            admins.Add(user);

            var result = await CreateService().RemoveUserFromRoleAsync("admin1", "SystemAdministrator", "admin1");

            result.Should().BeFalse();
            _userManager.Verify(m => m.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RemoveUserFromRole_SystemAdministratorWhenOthersExist_Succeeds()
        {
            var admins = new List<ApplicationUser>();
            var user = SetupUser("admin1", admins, true);
            admins.Add(user);
            admins.Add(new ApplicationUser { Id = "admin2" });

            var result = await CreateService().RemoveUserFromRoleAsync("admin1", "SystemAdministrator", "admin1");

            result.Should().BeTrue();
            _userManager.Verify(m => m.RemoveFromRoleAsync(user, "SystemAdministrator"), Times.Once);
        }

        [Fact]
        public async Task IsLastSystemAdministrator_ReflectsAdminCount()
        {
            var admins = new List<ApplicationUser>();
            var user = SetupUser("admin1", admins, true);
            admins.Add(user);
            var service = CreateService();

            (await service.IsLastSystemAdministratorAsync("admin1")).Should().BeTrue();

            admins.Add(new ApplicationUser { Id = "admin2" });
            (await service.IsLastSystemAdministratorAsync("admin1")).Should().BeFalse();
        }
    }
}
