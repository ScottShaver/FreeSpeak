using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies that the resource sets used by feed content reporting define the same keys in all 12 locales.
/// </summary>
public class FeedReportLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "SocialFeed")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb", "Resources");
    }

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .GroupBy(e => e.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.First().Element("value")?.Value ?? string.Empty);

    /// <summary>
    /// Every locale file has exactly the neutral keys, and every value is non-empty.
    /// </summary>
    /// <param name="relativeBase">Resource path relative to Resources, without extension.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(Data))]
    public void LocaleFile_HasSameKeysAsNeutral(string relativeBase, string locale)
    {
        var baseName = Path.Combine(ResourcesDir(), relativeBase.Replace('/', Path.DirectorySeparatorChar));
        var neutral = Entries(baseName + ".resx");
        var localized = Entries($"{baseName}.{locale}.resx");

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
        localized.Values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
    }

    /// <summary>
    /// The notification template used for the reporter outcome exists in the neutral file.
    /// </summary>
    [Fact]
    public void NotificationTemplates_ContainsFeedReportReviewed()
    {
        var neutral = Entries(Path.Combine(ResourcesDir(), "Notifications", "NotificationTemplates.resx"));

        neutral.Keys.Should().Contain(k => k.Contains("FeedReportReviewed"));
    }

    /// <summary>
    /// Supplies resource sets and locales.
    /// </summary>
    public static IEnumerable<object[]> Data() =>
        new[] { "SocialFeed/ReportFeedContentModal", "SocialFeed/PostEditModal", "Notifications/NotificationTemplates" }
            .SelectMany(r => Locales.Select(l => new object[] { r, l }));
}
