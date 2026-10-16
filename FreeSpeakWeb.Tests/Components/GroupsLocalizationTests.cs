using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Groups;
using FreeSpeakWeb.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies the localization resources of the Groups components and the localized RoleBadge rendering.
/// </summary>
public class GroupsLocalizationTests : TestContext
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Groups")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb", "Resources");
    }

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty);

    private static List<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v).ToList();

    /// <summary>
    /// Supplies every Groups resource (plus the shared comment display) and locale combination.
    /// </summary>
    public static IEnumerable<object[]> ResourceLocaleData()
    {
        var groups = Directory.GetFiles(Path.Combine(ResourcesDir(), "Groups"), "*.resx")
            .Select(Path.GetFileName)
            .Where(f => f!.Count(c => c == '.') == 1)
            .Select(f => "Groups/" + Path.GetFileNameWithoutExtension(f!));

        return groups.Append("SocialFeed/MultiLineCommentDisplay")
            .SelectMany(r => Locales.Select(l => new object[] { r, l }));
    }

    /// <summary>
    /// Every locale file has exactly the keys and placeholders of its neutral file.
    /// </summary>
    /// <param name="resource">The resource path relative to the Resources folder.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(ResourceLocaleData))]
    public void LocaleFile_MatchesNeutralKeysAndPlaceholders(string resource, string locale)
    {
        var neutral = Entries(Path.Combine(ResourcesDir(), resource + ".resx"));
        var localized = Entries(Path.Combine(ResourcesDir(), $"{resource}.{locale}.resx"));

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
        foreach (var (key, value) in neutral)
        {
            Placeholders(localized[key]).Should().Equal(Placeholders(value), $"placeholders of '{key}' must match");
        }
    }

    /// <summary>
    /// The role badge resolves its label from the localizer instead of hard-coded English.
    /// </summary>
    [Fact]
    public void RoleBadge_ShowText_UsesLocalizer()
    {
        Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));

        var cut = Render<RoleBadge>(p => p
            .Add(c => c.Role, RoleType.GroupAdmin)
            .Add(c => c.ShowText, true));

        cut.Markup.Should().Contain("GroupAdmin");
        cut.Markup.Should().NotContain("Group Admin");
    }
}
