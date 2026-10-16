using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies that every locale file of the Mod Log tab defines exactly the keys of the neutral resource file.
/// </summary>
public class ModerationLogTabLocalizationTests
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
        return Path.Combine(dir!.FullName, "FreeSpeakWeb", "Resources", "Groups");
    }

    private static HashSet<string> Keys(string path) =>
        XDocument.Load(path).Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();

    /// <summary>
    /// Every locale file has the same keys as the neutral file.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_HasSameKeysAsNeutral(string locale)
    {
        var dir = ResourcesDir();
        var neutral = Keys(Path.Combine(dir, "ModerationLogTab.resx"));
        var localized = Keys(Path.Combine(dir, $"ModerationLogTab.{locale}.resx"));

        localized.Should().BeEquivalentTo(neutral);
    }

    /// <summary>
    /// Removal actions offered as filters have localized labels in every locale.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_HasRemovalActionLabels(string locale)
    {
        var keys = Keys(Path.Combine(ResourcesDir(), $"ModerationLogTab.{locale}.resx"));

        keys.Should().Contain(new[] { "Action_PostRemoved", "Action_CommentRemoved" });
    }

    /// <summary>
    /// Supplies the locale codes.
    /// </summary>
    public static IEnumerable<object[]> LocaleData() => Locales.Select(l => new object[] { l });
}
