using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Groups;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// bUnit tests for the <see cref="ModerationLogTab"/> component.
/// </summary>
public class ModerationLogTabTests : TestContext
{
    private readonly Mock<IGroupModerationLogService> _service = new();

    /// <summary>
    /// Registers the services the component depends on.
    /// </summary>
    public ModerationLogTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton(_service.Object);
        Services.AddSingleton<IOptions<GroupModerationLogSettings>>(Options.Create(new GroupModerationLogSettings()));
        Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        Services.AddSingleton<TimestampFormattingService>();
    }

    private static GroupModerationLogEntryDto Entry(int id, GroupModerationAction action = GroupModerationAction.PostApproved) =>
        new(id, 1, "actor", "Mod Alice", action, GroupModerationTargetType.Post, "5", "t", "Bob", "Some summary", "Some reason", DateTime.UtcNow.AddMinutes(-5));

    private IRenderedComponent<ModerationLogTab> Render() =>
        Render<ModerationLogTab>(p => p.Add(c => c.Group, new Group { Id = 1, Name = "G", Description = "D", CreatorId = "c" }).Add(c => c.CurrentUserId, "actor"));

    /// <summary>
    /// Entries returned by the service are rendered with actor, target and reason.
    /// </summary>
    [Fact]
    public void Renders_Entries()
    {
        _service.Setup(s => s.GetLogAsync(1, "actor", It.IsAny<GroupModerationLogFilter>(), 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(new[] { Entry(1), Entry(2, GroupModerationAction.MemberBanned) }, 2, true));

        var cut = Render();

        cut.FindAll(".modlog-entry").Should().HaveCount(2);
        cut.Markup.Should().Contain("Action_PostApproved").And.Contain("Action_MemberBanned").And.Contain("ReasonLabel");
    }

    /// <summary>
    /// An empty log shows the empty state and no export is possible.
    /// </summary>
    [Fact]
    public void Shows_EmptyState()
    {
        _service.Setup(s => s.GetLogAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<GroupModerationLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(Array.Empty<GroupModerationLogEntryDto>(), 0, true));

        var cut = Render();

        cut.Markup.Should().Contain("NoEntries");
        cut.Find("button.btn-outline-primary").HasAttribute("disabled").Should().BeTrue();
    }

    /// <summary>
    /// An unauthorized response shows the not-authorized message and hides the export button.
    /// </summary>
    [Fact]
    public void Shows_NotAuthorized_And_HidesExport()
    {
        _service.Setup(s => s.GetLogAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<GroupModerationLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(Array.Empty<GroupModerationLogEntryDto>(), 0, false));

        var cut = Render();

        cut.Markup.Should().Contain("NotAuthorized");
        cut.FindAll("button.btn-outline-primary").Should().BeEmpty();
    }

    /// <summary>
    /// Load more requests the next page and appends entries.
    /// </summary>
    [Fact]
    public void LoadMore_AppendsNextPage()
    {
        var first = Enumerable.Range(1, 25).Select(i => Entry(i)).ToList();
        _service.Setup(s => s.GetLogAsync(1, "actor", It.IsAny<GroupModerationLogFilter>(), 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(first, 26, true));
        _service.Setup(s => s.GetLogAsync(1, "actor", It.IsAny<GroupModerationLogFilter>(), 2, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(new[] { Entry(26) }, 26, true));

        var cut = Render();
        cut.FindAll(".modlog-entry").Should().HaveCount(25);

        cut.FindAll("button").First(b => b.TextContent.Contains("LoadMore")).Click();

        cut.FindAll(".modlog-entry").Should().HaveCount(26);
        cut.FindAll("button").Any(b => b.TextContent.Contains("LoadMore")).Should().BeFalse();
    }

    /// <summary>
    /// Selecting an action filter reloads the log using that action.
    /// </summary>
    [Fact]
    public void ActionFilter_ReloadsWithAction()
    {
        _service.Setup(s => s.GetLogAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<GroupModerationLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(new[] { Entry(1) }, 1, true));

        var cut = Render();
        cut.Find("#modlog-action").Change(GroupModerationAction.MemberBanned.ToString());

        _service.Verify(s => s.GetLogAsync(1, "actor", It.Is<GroupModerationLogFilter>(f => f.Action == GroupModerationAction.MemberBanned), 1, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The export button opens the export endpoint for the group.
    /// </summary>
    [Fact]
    public void Export_OpensEndpoint()
    {
        _service.Setup(s => s.GetLogAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<GroupModerationLogFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupModerationLogPage(new[] { Entry(1) }, 1, true));

        var cut = Render();
        cut.Find("button.btn-outline-primary").Click();

        JSInterop.Invocations.Should().Contain(i => i.Identifier == "open" && i.Arguments[0]!.ToString()!.Contains("/api/groups/1/moderation-log/export"));
    }
}
