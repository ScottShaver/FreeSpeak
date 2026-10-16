using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using FreeSpeakWeb.Components.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// bUnit tests for <see cref="SessionEndGuard"/>, which cuts off open circuits whose user loses authentication.
    /// </summary>
    public class SessionEndGuardTests : BunitContext
    {
        /// <summary>
        /// An authenticated circuit that becomes anonymous must be force-reloaded.
        /// </summary>
        [Fact]
        public void AuthenticatedUserBecomingAnonymous_ForcesReload()
        {
            var auth = AddAuthorization();
            auth.SetAuthorized("alice");
            Render<SessionEndGuard>();
            var nav = Services.GetRequiredService<BunitNavigationManager>();
            var before = nav.History.Count;

            auth.SetNotAuthorized();

            nav.History.Count.Should().BeGreaterThan(before);
            nav.History.First().Options.ForceLoad.Should().BeTrue();
        }

        /// <summary>
        /// An anonymous visitor must never be redirected by the guard.
        /// </summary>
        [Fact]
        public void AnonymousUser_IsNotRedirected()
        {
            var auth = AddAuthorization();
            auth.SetNotAuthorized();
            Render<SessionEndGuard>();
            var nav = Services.GetRequiredService<BunitNavigationManager>();

            auth.SetNotAuthorized();

            nav.History.Should().BeEmpty();
        }
    }
}
