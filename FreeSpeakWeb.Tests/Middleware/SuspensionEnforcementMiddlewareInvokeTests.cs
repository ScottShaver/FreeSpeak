using System.Security.Claims;
using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Middleware;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Middleware
{
    /// <summary>
    /// Tests that the suspension enforcement middleware ends the session of suspended users on every kind of request.
    /// </summary>
    public class SuspensionEnforcementMiddlewareInvokeTests
    {
        private readonly Mock<IUserModerationService> _moderation = new();
        private readonly Mock<SignInManager<ApplicationUser>> _signIn;
        private bool _nextCalled;

        /// <summary>
        /// Creates the mocks the middleware needs.
        /// </summary>
        public SuspensionEnforcementMiddlewareInvokeTests()
        {
            var store = Mock.Of<IUserStore<ApplicationUser>>();
            var userManager = new Mock<UserManager<ApplicationUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
            _signIn = new Mock<SignInManager<ApplicationUser>>(
                userManager.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<ApplicationUser>>.Instance,
                Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
                Mock.Of<IUserConfirmation<ApplicationUser>>());
            _signIn.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);
        }

        private async Task<DefaultHttpContext> RunAsync(string path, string method, bool authenticated)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Method = method;
            if (authenticated)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "u1"),
                    new Claim(ClaimTypes.Name, "target user")
                }, "test"));
            }

            _nextCalled = false;
            var middleware = new SuspensionEnforcementMiddleware(_ => { _nextCalled = true; return Task.CompletedTask; }, NullLogger<SuspensionEnforcementMiddleware>.Instance);
            await middleware.InvokeAsync(context, _moderation.Object, _signIn.Object);
            return context;
        }

        private void Suspended() =>
            _moderation.Setup(m => m.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActiveSuspensionInfo("bad", DateTime.UtcNow.AddDays(1)));

        /// <summary>A suspended user's page request is signed out and redirected to the lockout page.</summary>
        [Fact]
        public async Task SuspendedUser_PageRequest_IsSignedOutAndRedirected()
        {
            Suspended();

            var context = await RunAsync("/feed", "GET", true);

            _nextCalled.Should().BeFalse();
            _signIn.Verify(s => s.SignOutAsync(), Times.Once);
            context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
            context.Response.Headers.Location.ToString().Should().StartWith("/Account/Lockout?username=target%20user");
        }

        /// <summary>A suspended user cannot reach API endpoints, including ones whose path ends in a file name.</summary>
        [Theory]
        [InlineData("/api/secure-files/post-video/u1/v1/clip.mp4", "GET")]
        [InlineData("/api/group-files/5/download", "GET")]
        [InlineData("/api/groups/1/moderation-log/export", "GET")]
        public async Task SuspendedUser_ApiRequest_IsRefused(string path, string method)
        {
            Suspended();

            await RunAsync(path, method, true);

            _nextCalled.Should().BeFalse();
        }

        /// <summary>A suspended user's non-GET request (for example the Blazor negotiate POST) gets 401 and is not processed.</summary>
        [Fact]
        public async Task SuspendedUser_PostRequest_Gets401()
        {
            Suspended();

            var context = await RunAsync("/_blazor/negotiate", "POST", true);

            _nextCalled.Should().BeFalse();
            context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        /// <summary>The lockout page itself stays reachable.</summary>
        [Fact]
        public async Task SuspendedUser_LockoutPage_IsReachable()
        {
            Suspended();

            await RunAsync("/Account/Lockout", "GET", true);

            _nextCalled.Should().BeTrue();
            _signIn.Verify(s => s.SignOutAsync(), Times.Never);
        }

        /// <summary>Users who are not suspended and anonymous visitors pass through untouched.</summary>
        [Fact]
        public async Task NonSuspendedAndAnonymous_PassThrough()
        {
            _moderation.Setup(m => m.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((ActiveSuspensionInfo?)null);

            await RunAsync("/feed", "GET", true);
            _nextCalled.Should().BeTrue();

            await RunAsync("/feed", "GET", false);
            _nextCalled.Should().BeTrue();
            _moderation.Verify(m => m.GetActiveSuspensionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>After the suspension ends the user passes through again.</summary>
        [Fact]
        public async Task ExpiredSuspension_AllowsUserBack()
        {
            Suspended();
            await RunAsync("/feed", "GET", true);
            _nextCalled.Should().BeFalse();

            _moderation.Reset();
            _moderation.Setup(m => m.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync((ActiveSuspensionInfo?)null);
            await RunAsync("/feed", "GET", true);

            _nextCalled.Should().BeTrue();
        }
    }
}
