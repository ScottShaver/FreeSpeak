using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Shared;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    public class PopupMenuTests : TestContext
    {
        public PopupMenuTests()
        {
            Services.AddSingleton<PopupPortalService>();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CloseFromKeyboardAsync_InvokesOnClose(bool usePortal)
        {
            var closed = 0;
            var cut = Render<PopupMenu>(p => p
                .Add(x => x.IsVisible, true)
                .Add(x => x.UsePortal, usePortal)
                .Add(x => x.AnchorElementId, "anchor")
                .Add(x => x.MenuItems, new List<PopupMenu.PopupMenuItem> { new() { Text = "Edit" } })
                .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => closed++)));

            await cut.InvokeAsync(() => cut.Instance.CloseFromKeyboardAsync());

            closed.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void Visible_RegistersKeyboardHandler_AndHiddenUnregisters()
        {
            var cut = Render<PopupMenu>(p => p
                .Add(x => x.IsVisible, true)
                .Add(x => x.UsePortal, false)
                .Add(x => x.AnchorElementId, "anchor"));

            cut.WaitForAssertion(() => JSInterop.Invocations.Any(i => i.Identifier == "register").Should().BeTrue());

            cut.Render(p => p.Add(x => x.IsVisible, false));

            cut.WaitForAssertion(() => JSInterop.Invocations.Any(i => i.Identifier == "unregister").Should().BeTrue());
        }

        [Fact]
        public void InlineMenu_RendersMenuRoles()
        {
            var cut = Render<PopupMenu>(p => p
                .Add(x => x.IsVisible, true)
                .Add(x => x.UsePortal, false)
                .Add(x => x.MenuItems, new List<PopupMenu.PopupMenuItem> { new() { Text = "Edit" } }));

            cut.Find(".popup-menu").GetAttribute("role").Should().Be("menu");
            cut.Find(".popup-menu-item").GetAttribute("role").Should().Be("menuitem");
        }
    }
}
