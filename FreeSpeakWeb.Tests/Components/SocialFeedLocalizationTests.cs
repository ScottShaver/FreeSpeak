using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies the localization resources of the social feed components.
/// </summary>
public class SocialFeedLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string FeedResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "SocialFeed")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb", "Resources", "SocialFeed");
    }

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty);

    private static List<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v).ToList();

    /// <summary>
    /// Supplies every social feed resource and locale combination.
    /// </summary>
    public static IEnumerable<object[]> ResourceLocaleData() =>
        Directory.GetFiles(FeedResourcesDir(), "*.resx")
            .Select(f => Path.GetFileName(f)!)
            .Where(f => f.Count(c => c == '.') == 1)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .SelectMany(r => Locales.Select(l => new object[] { r, l }));

    /// <summary>
    /// Supplies every social feed resource name.
    /// </summary>
    public static IEnumerable<object[]> ResourceData() =>
        ResourceLocaleData().Select(o => (string)o[0]).Distinct().Select(r => new object[] { r });

    /// <summary>
    /// Every locale file has exactly the keys and placeholders of its neutral file.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(ResourceLocaleData))]
    public void LocaleFile_MatchesNeutralKeysAndPlaceholders(string resource, string locale)
    {
        var neutral = Entries(Path.Combine(FeedResourcesDir(), resource + ".resx"));
        var localized = Entries(Path.Combine(FeedResourcesDir(), $"{resource}.{locale}.resx"));

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
        foreach (var (key, value) in neutral)
        {
            Placeholders(localized[key]).Should().Equal(Placeholders(value), $"placeholders of '{key}' must match");
        }
    }

    /// <summary>
    /// The Designer file exposes every key of the neutral resource.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    [Theory]
    [MemberData(nameof(ResourceData))]
    public void DesignerFile_ContainsEveryNeutralKey(string resource)
    {
        var designer = File.ReadAllText(Path.Combine(FeedResourcesDir(), resource + ".Designer.cs"));
        var neutral = Entries(Path.Combine(FeedResourcesDir(), resource + ".resx"));

        foreach (var key in neutral.Keys)
        {
            designer.Should().Contain($"GetString(\"{key}\"", $"Designer must expose '{key}'");
        }
    }

    /// <summary>
    /// Previously hard-coded feed strings are translated in every locale.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="key">The resource key.</param>
    [Theory]
    [InlineData("PostDetailModal", "LoadingComments")]
    [InlineData("PostDetailModal", "LoadingMoreComments")]
    [InlineData("PostDetailModal", "NoMoreCommentsAllowed")]
    [InlineData("PostCreator", "SelectedImageAlt")]
    [InlineData("FeedArticle", "ReportSubmitted")]
    [InlineData("FeedArticle", "FailedToUpdateNotificationSettings")]
    [InlineData("FeedArticle", "FailedToDeletePost")]
    [InlineData("FeedArticle", "DeletePostFullConfirmMessage")]
    public void NewKeys_AreTranslatedInEveryLocale(string resource, string key)
    {
        var english = Entries(Path.Combine(FeedResourcesDir(), resource + ".resx"))[key];

        foreach (var locale in Locales)
        {
            Entries(Path.Combine(FeedResourcesDir(), $"{resource}.{locale}.resx"))[key]
                .Should().NotBe(english, $"'{key}' must be translated for {locale}");
        }
    }
}
