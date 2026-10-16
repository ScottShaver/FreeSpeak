using FluentAssertions;
using FreeSpeakWeb.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FreeSpeakWeb.Tests.Middleware
{
    /// <summary>
    /// Tests for the suspension enforcement middleware path rules.
    /// </summary>
    public class SuspensionEnforcementMiddlewareTests
    {
        /// <summary>Pages a suspended user must reach and static assets are not checked.</summary>
        [Theory]
        [InlineData("/Account/Lockout", true)]
        [InlineData("/account/logout", true)]
        [InlineData("/_framework/blazor.web.js", true)]
        [InlineData("/_content/lib/x", true)]
        [InlineData("/css/app.css", true)]
        [InlineData("/api/secure-files/post-video/u1/v1/clip.mp4", false)]
        [InlineData("/API/secure-files/post-image/u1/i1/a.jpg", false)]
        [InlineData("/api/group-files/5/download", false)]
        [InlineData("/", false)]
        [InlineData("/feed", false)]
        [InlineData("/Account/Login", false)]
        public void IsExempt_MatchesExpectedPaths(string path, bool expected)
        {
            SuspensionEnforcementMiddleware.IsExempt(new PathString(path)).Should().Be(expected);
        }
    }
}
