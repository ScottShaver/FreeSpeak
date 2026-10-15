using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using FreeSpeakWeb.Components.Pages;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services.Settings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// Authentication state provider returning a fixed authenticated user.
    /// </summary>
    internal sealed class StubAuthProvider : AuthenticationStateProvider
    {
        /// <inheritdoc/>
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "admin") }, "test"))));
    }

    /// <summary>
    /// bUnit tests for the metadata-driven system settings group page.
    /// </summary>
    public class SystemSettingsGroupTests : TestContext
    {
        private readonly Mock<ISystemSettingsService> _service = new();
        private readonly Mock<IEmailTestService> _emailTest = new();

        public SystemSettingsGroupTests()
        {
            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("admin");

            Services.AddSingleton(_service.Object);
            Services.AddSingleton(_emailTest.Object);
            Services.AddSingleton(userManager.Object);
            Services.AddSingleton<IStringLocalizer<FreeSpeakWeb.Resources.Pages.SystemSettings>>(
                new PassthroughLocalizer<FreeSpeakWeb.Resources.Pages.SystemSettings>());
            Services.AddSingleton(NullLogger<SystemSettingsGroup>.Instance);
            Services.AddSingleton<AuthenticationStateProvider>(new StubAuthProvider());
        }

        private void Setup(string groupId, Func<SettingFieldDefinition, bool> isSet)
        {
            var group = SettingsCatalog.Find(groupId)!;
            var fields = group.Fields
                .Select(f => new SettingFieldState(f, f.IsSecret ? "" : f.Default, isSet(f), false))
                .ToList();
            _service.Setup(s => s.GetGroupAsync(groupId, "admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SettingGroupState(group, fields));
        }

        /// <summary>Verifies the form is rendered from the catalog metadata.</summary>
        [Fact]
        public void Renders_FieldsFromMetadata()
        {
            Setup("virusscan", _ => false);

            var cut = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "virusscan"));

            foreach (var field in SettingsCatalog.Find("virusscan")!.Fields)
            {
                cut.Find($"[data-testid='field-{field.Key}']").Should().NotBeNull();
            }
        }

        /// <summary>Verifies secret values are never rendered, only their set state.</summary>
        [Fact]
        public void Secret_ValueIsNeverRendered()
        {
            var group = SettingsCatalog.Find("smtp")!;
            var fields = group.Fields
                .Select(f => new SettingFieldState(f, f.IsSecret ? "top-secret" : f.Default, f.IsSecret, true))
                .ToList();
            _service.Setup(s => s.GetGroupAsync("smtp", "admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SettingGroupState(group, fields));

            var cut = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "smtp"));

            cut.Markup.Should().NotContain("top-secret");
            cut.Markup.Should().Contain("SecretSet");
        }

        /// <summary>Verifies the restart banner appears only for restart-required groups.</summary>
        [Fact]
        public void RestartBanner_OnlyForRestartGroups()
        {
            Setup("ratelimiting", _ => false);
            Setup("virusscan", _ => false);

            var restart = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "ratelimiting"));
            var live = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "virusscan"));

            restart.FindAll("[data-testid='restart-banner']").Should().HaveCount(1);
            live.FindAll("[data-testid='restart-banner']").Should().BeEmpty();
        }

        /// <summary>Verifies validation errors from the service are shown next to the field.</summary>
        [Fact]
        public void ValidationErrors_AreDisplayed()
        {
            Setup("virusscan", _ => false);
            _service.Setup(s => s.SaveGroupAsync("virusscan", It.IsAny<IReadOnlyDictionary<string, string?>>(), "admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SaveSettingsResult(false,
                    new[] { new SettingValidationError("VirusScan:ClamAvPort", "E_Required") },
                    Array.Empty<string>(), false));
            var cut = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "virusscan"));

            cut.Find("[data-testid='settings-form']").Submit();

            cut.Find("[data-testid='error-VirusScan:ClamAvPort']").TextContent.Should().Contain("E_Required");
        }

        /// <summary>Verifies the test email card appears only for the Smtp group and the button is disabled until a recipient is entered.</summary>
        [Fact]
        public void TestEmail_OnlyForSmtp_AndButtonDisabledWithoutRecipient()
        {
            Setup("smtp", _ => false);
            Setup("virusscan", _ => false);

            var smtp = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "smtp"));
            var other = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "virusscan"));

            other.FindAll("[data-testid='test-email-card']").Should().BeEmpty();
            smtp.Find("[data-testid='test-email-send']").HasAttribute("disabled").Should().BeTrue();
            smtp.Find("[data-testid='test-email-recipient']").Input("x@example.com");
            smtp.Find("[data-testid='test-email-send']").HasAttribute("disabled").Should().BeFalse();
        }

        /// <summary>Verifies the test button reports success and sanitized failure keys.</summary>
        [Fact]
        public void TestEmail_ShowsSuccessAndFailure()
        {
            Setup("smtp", _ => false);
            _emailTest.Setup(s => s.SendTestEmailAsync("ok@example.com", "admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestEmailResult(true, null));
            _emailTest.Setup(s => s.SendTestEmailAsync("bad@example.com", "admin", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestEmailResult(false, "E_SmtpAuth"));
            var cut = Render<SystemSettingsGroup>(p => p.Add(c => c.GroupId, "smtp"));

            cut.Find("[data-testid='test-email-recipient']").Input("ok@example.com");
            cut.Find("[data-testid='test-email-send']").Click();
            cut.Find("[data-testid='result-message']").TextContent.Should().Contain("TestEmailSuccess");

            cut.Find("[data-testid='test-email-recipient']").Input("bad@example.com");
            cut.Find("[data-testid='test-email-send']").Click();
            cut.Find("[data-testid='result-message']").TextContent.Should().Contain("TestEmailFailed");
            cut.Find("[data-testid='result-message']").ClassList.Should().Contain("alert-danger");
        }
    }
}
