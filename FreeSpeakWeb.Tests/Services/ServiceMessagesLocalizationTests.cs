using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using FreeSpeakWeb.Services;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

/// <summary>
/// Verifies the localization resources used by service result messages.
/// </summary>
public class ServiceMessagesLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Services")))
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
    /// Supplies every service/repository message resource and locale combination.
    /// </summary>
    public static IEnumerable<object[]> ResourceLocaleData()
    {
        var root = ResourcesDir();
        var neutrals = Directory.GetFiles(Path.Combine(root, "Services"), "*.resx", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "Repositories"), "*.resx", SearchOption.AllDirectories))
            .Where(f => Path.GetFileName(f).Count(c => c == '.') == 1)
            .Select(f => Path.GetRelativePath(root, f)[..^".resx".Length].Replace('\\', '/'))
            .Append("Shared/TimeFormatting");

        return neutrals.SelectMany(r => Locales.Select(l => new object[] { r, l }));
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
    /// Without an injected localizer, a service falls back to the neutral English text.
    /// </summary>
    [Fact]
    public void ServiceLocalizer_WithoutInjection_ReturnsEnglish()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var localizer = ServiceLocalizer.Resolve<ClamAvVirusScanService>(null);

            localizer["UnknownVirusName"].Value.Should().Be("Unknown");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    /// <summary>
    /// The resolved localizer follows the current UI culture.
    /// </summary>
    [Fact]
    public void ServiceLocalizer_GermanCulture_ReturnsGerman()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            var localizer = ServiceLocalizer.Resolve<GroupFileService>(null);

            localizer["FileNotFound"].Value.Should().NotBe("File not found.");
            localizer["FileNotFound"].ResourceNotFound.Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
