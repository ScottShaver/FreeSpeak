using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies that every locale file of the Pages area defines exactly the keys of its neutral resource file.
/// </summary>
public class PagesLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static readonly string[] Pages =
    {
        "Home", "PublicHome", "Notifications", "MyUploads", "SinglePost", "SingleGroupPost",
        "FriendDetails", "FriendsList", "Groups", "GroupView", "GroupFilesPage"
    };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Pages")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb", "Resources", "Pages");
    }

    private static HashSet<string> Keys(string path) =>
        XDocument.Load(path).Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();

    /// <summary>
    /// Every locale file has the same keys as the neutral file.
    /// </summary>
    /// <param name="page">The page resource name.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(PageLocaleData))]
    public void LocaleFile_HasSameKeysAsNeutral(string page, string locale)
    {
        var dir = ResourcesDir();
        var neutral = Keys(Path.Combine(dir, $"{page}.resx"));
        var localized = Keys(Path.Combine(dir, $"{page}.{locale}.resx"));

        localized.Should().BeEquivalentTo(neutral);
    }

    /// <summary>
    /// Supplies every page and locale combination.
    /// </summary>
    public static IEnumerable<object[]> PageLocaleData() =>
        Pages.SelectMany(p => Locales.Select(l => new object[] { p, l }));
}
