using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies the localization resources used by the Account/Manage pages .
/// </summary>
public class AccountManageLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Account")))
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
    /// Every locale file has exactly the neutral keys, non-empty values and the same format placeholders.
    /// </summary>
    /// <param name="relativeBase">Resource path relative to Resources, without extension.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(Data))]
    public void LocaleFile_MatchesNeutral(string relativeBase, string locale)
    {
        var baseName = Path.Combine(ResourcesDir(), relativeBase.Replace('/', Path.DirectorySeparatorChar));
        var neutral = Entries(baseName + ".resx");
        var localized = Entries($"{baseName}.{locale}.resx");

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
        localized.Values.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
        foreach (var (key, value) in neutral)
        {
            Placeholders(localized[key]).Should().BeEquivalentTo(Placeholders(value), $"key {key} in {locale}");
        }
    }

    /// <summary>
    /// Localized error messages must start with the locale's error prefix so the status message is styled as an error.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(LocaleData))]
    public void ErrorMessages_StartWithLocalizedPrefix(string locale)
    {
        var prefix = Entries(Path.Combine(ResourcesDir(), "Account", "Shared", $"StatusMessage.{locale}.resx"))["ErrorPrefix"];
        var set = Entries(Path.Combine(ResourcesDir(), "Account", "Pages", "Manage", $"ExternalLogins.{locale}.resx"));

        set["ErrorLoginNotRemoved"].Should().StartWith(prefix);
        set["ErrorCouldNotLoadInfo"].Should().StartWith(prefix);
        set["ErrorLoginNotAdded"].Should().StartWith(prefix);
    }

    /// <summary>
    /// The helper encodes link text and href and formats the template.
    /// </summary>

    private static IEnumerable<string> Placeholders(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v);

    /// <summary>
    /// Supplies resource sets and locales.
    /// </summary>
    public static IEnumerable<object[]> Data() =>
        new[]
        {
            "Account/Pages/Manage/ManageAccount", "Account/Pages/Manage/ChangePassword", "Account/Pages/Manage/Disable2fa",
            "Account/Pages/Manage/GenerateRecoveryCodes", "Account/Pages/Manage/ResetAuthenticator", "Account/Pages/Manage/ExternalLogins",
            "Account/Pages/Manage/EnableAuthenticator", "Account/Shared/ShowRecoveryCodes", "Account/Shared/StatusMessage",
            "Account/Shared/UserPreferencesComponent"
        }.SelectMany(r => Locales.Select(l => new object[] { r, l }));

    /// <summary>
    /// Supplies locales.
    /// </summary>
    public static IEnumerable<object[]> LocaleData() => Locales.Select(l => new object[] { l });
}
