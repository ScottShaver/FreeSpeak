using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Account.Pages;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// bUnit tests for the Lockout page, which is reachable without authentication.
    /// </summary>
    public class LockoutPageTests : TestContext
    {
        private const string SecretReason = "SECRET-MODERATION-REASON";

        private readonly Mock<IUserModerationService> _moderation = new();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;

        /// <summary>
        /// Registers the mocked services needed by the page.
        /// </summary>
        public LockoutPageTests()
        {
            _userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            var auth = new Mock<AuthenticationStateProvider>();
            auth.Setup(a => a.GetAuthenticationStateAsync())
                .ReturnsAsync(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
            Services.AddSingleton(_userManager.Object);
            Services.AddSingleton(auth.Object);
            Services.AddSingleton(_moderation.Object);
            Services.AddSingleton(Options.Create(new SiteSettings()));
            Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
            Services.AddLogging();
        }

        private IRenderedComponent<Lockout> RenderLockout()
        {
            var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
            nav.NavigateTo(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(nav.Uri, "Username", "victim"));
            return Render<Lockout>();
        }

        private void ArrangeSuspendedUser(DateTime? endsAt)
        {
            var user = new ApplicationUser { Id = "u1", UserName = "victim" };
            _userManager.Setup(m => m.FindByNameAsync("victim")).ReturnsAsync(user);
            _moderation.Setup(m => m.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActiveSuspensionInfo(SecretReason, endsAt));
        }

        /// <summary>A visitor who only knows the username never sees the moderator's reason.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Lockout_UnauthenticatedVisitor_DoesNotSeeReason(bool permanent)
        {
            ArrangeSuspendedUser(permanent ? null : DateTime.UtcNow.AddDays(2));

            var cut = RenderLockout();

            cut.FindAll("[data-testid=suspension-card]").Should().HaveCount(1);
            cut.FindAll("[data-testid=suspension-reason]").Should().BeEmpty();
            cut.Markup.Should().NotContain(SecretReason);
        }

        /// <summary>Only the end date is shown for a timed suspension, and none for a permanent one.</summary>
        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        public void Lockout_ShowsEndDateOnlyForTimedSuspension(bool permanent, int expectedUntil)
        {
            ArrangeSuspendedUser(permanent ? null : DateTime.UtcNow.AddDays(2));

            var cut = RenderLockout();

            cut.FindAll("[data-testid=suspension-until]").Should().HaveCount(expectedUntil);
        }
    }
}
