using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies the localization of the system administration page: key parity across locales,
/// placeholder parity, real translations and that every key used by the markup exists.
/// </summary>
public class SystemAdminLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Pages")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return dir!.FullName;
    }

    private static string ResxPath(string? locale) =>
        Path.Combine(RepoRoot(), "FreeSpeakWeb", "Resources", "Pages", locale == null ? "SystemAdmin.resx" : $"SystemAdmin.{locale}.resx");

    private static Dictionary<string, string> Load(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value);

    /// <summary>
    /// Every locale file defines exactly the keys of the neutral file.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_HasSameKeysAsNeutral(string locale)
    {
        Load(ResxPath(locale)).Keys.Should().BeEquivalentTo(Load(ResxPath(null)).Keys);
    }

    /// <summary>
    /// Format placeholders in each locale match the neutral value and no value is empty.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_PreservesPlaceholdersAndIsNotEmpty(string locale)
    {
        var neutral = Load(ResxPath(null));
        var localized = Load(ResxPath(locale));
        var placeholder = new Regex(@"\{\d+\}");

        foreach (var (key, value) in neutral)
        {
            localized[key].Should().NotBeNullOrWhiteSpace($"{key} must be translated in {locale}");
            placeholder.Matches(localized[key]).Select(m => m.Value).OrderBy(x => x)
                .Should().Equal(placeholder.Matches(value).Select(m => m.Value).OrderBy(x => x), $"{key} placeholders in {locale}");
        }
    }

    /// <summary>
    /// Newly added card text must be translated rather than copied from English.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_CardDescriptionsAreTranslated(string locale)
    {
        var neutral = Load(ResxPath(null));
        var localized = Load(ResxPath(locale));

        foreach (var key in neutral.Keys.Where(k => k.EndsWith("Description", StringComparison.Ordinal)))
        {
            localized[key].Should().NotBe(neutral[key], $"{key} must be translated in {locale}");
        }

        localized["ComingSoon"].Should().NotBe(neutral["ComingSoon"]);
    }

    /// <summary>
    /// Every localizer key referenced by SystemAdmin.razor exists in the neutral resource file,
    /// and the markup no longer contains the audited hard-coded strings.
    /// </summary>
    [Fact]
    public void SystemAdminRazor_UsesOnlyDefinedKeysAndNoHardCodedText()
    {
        var razor = File.ReadAllText(Path.Combine(RepoRoot(), "FreeSpeakWeb", "Components", "Pages", "SystemAdmin.razor"));
        var neutral = Load(ResxPath(null));

        var used = Regex.Matches(razor, @"(?<!Settings)Localizer\[""(\w+)""")
            .Select(m => m.Groups[1].Value).Distinct();
        used.Where(k => !neutral.ContainsKey(k)).Should().BeEmpty();

        string[] hardCoded =
        {
            "Coming Soon", "Additional Features", "Create New User", "Search Logs",
            "Showing first 100 results", "Start typing in the search box", "An error occurred while searching",
            "alt=\"Profile\"", "title=\"Actions\""
        };
        foreach (var text in hardCoded)
        {
            razor.Should().NotContain(text);
        }
    }

    /// <summary>
    /// Supplies every locale code.
    /// </summary>
    public static IEnumerable<object[]> LocaleData() => Locales.Select(l => new object[] { l });
}
