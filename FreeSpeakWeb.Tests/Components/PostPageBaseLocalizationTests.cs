using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies that the toast messages raised by <c>PostPageBase</c> are localized in all 12 locales
/// and that the base class no longer contains hard-coded user-facing text.
/// </summary>
public class PostPageBaseLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string WebDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Pages", "Base")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb");
    }

    private static string ResxPath(string? locale) =>
        Path.Combine(WebDir(), "Resources", "Pages", "Base", locale == null ? "PostPageBase.resx" : $"PostPageBase.{locale}.resx");

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty);

    /// <summary>
    /// Every locale file has exactly the neutral keys, non-empty values and the neutral placeholders.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_HasSameKeysAndPlaceholdersAsNeutral(string locale)
    {
        var neutral = Entries(ResxPath(null));
        var localized = Entries(ResxPath(locale));

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
        localized.Values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
        foreach (var key in neutral.Keys)
        {
            Regex.Matches(localized[key], @"\{\d+\}").Select(m => m.Value).Should()
                .BeEquivalentTo(Regex.Matches(neutral[key], @"\{\d+\}").Select(m => m.Value), key);
        }
    }

    /// <summary>
    /// Locale values must differ from the English text (no copied placeholders).
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void LocaleFile_IsTranslated(string locale)
    {
        var neutral = Entries(ResxPath(null));
        var localized = Entries(ResxPath(locale));

        foreach (var key in neutral.Keys)
        {
            localized[key].Should().NotBe(neutral[key], key);
        }
    }

    /// <summary>
    /// The base class uses only localized keys that exist in the neutral file and has no literal toast text.
    /// </summary>
    [Fact]
    public void PostPageBase_UsesOnlyExistingKeysAndNoLiteralToasts()
    {
        var source = File.ReadAllText(Path.Combine(WebDir(), "Components", "Pages", "Base", "PostPageBase.cs"));
        var neutral = Entries(ResxPath(null));

        var used = Regex.Matches(source, @"BaseLocalizer\[""(\w+)""\]").Select(m => m.Groups[1].Value).Distinct().ToList();

        used.Should().NotBeEmpty();
        neutral.Keys.Should().Contain(used);
        neutral.Keys.Should().BeEquivalentTo(used, "every resource key should be used by the base class");
        source.Should().NotMatchRegex(@"ShowError\(\s*""");
        source.Should().NotMatchRegex(@"\?\?\s*""");
    }

    /// <summary>
    /// Compiled satellite resources resolve the localized toast text at runtime.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void CompiledResources_ResolveLocalizedText(string locale)
    {
        var rm = FreeSpeakWeb.Resources.Pages.Base.PostPageBase.ResourceManager;
        var english = rm.GetString("AddReactionError", System.Globalization.CultureInfo.InvariantCulture);
        var localized = rm.GetString("AddReactionError", System.Globalization.CultureInfo.GetCultureInfo(locale));

        english.Should().Be("An error occurred while adding your reaction.");
        localized.Should().NotBeNullOrWhiteSpace().And.NotBe(english);
    }

    /// <summary>
    /// Supplies the locale codes.
    /// </summary>
    public static IEnumerable<object[]> LocaleData() => Locales.Select(l => new object[] { l });
}
