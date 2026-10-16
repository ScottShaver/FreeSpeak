using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies the resource sets used by user warnings and suspensions in all 12 locales: same keys,
/// non-empty values, the same format placeholders as the neutral text, and a matching Designer file.
/// </summary>
public class UserModerationLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };
    private static readonly string[] NotificationKeys = { "UserWarned", "UserSuspended", "UserSuspendedPermanent", "UserSuspensionLifted" };

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Moderation")))
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

    private static string[] Placeholders(string value) =>
        Regex.Matches(value, @"\{[^}]*\}").Select(m => m.Value).OrderBy(v => v).ToArray();

    /// <summary>Supplies resource sets and locales.</summary>
    public static IEnumerable<object[]> Data() =>
        new[] { "Moderation/UserModeration", "Notifications/NotificationTemplates", "Pages/SystemSettings" }
            .SelectMany(r => Locales.Select(l => new object[] { r, l }));

    /// <summary>Every locale has exactly the neutral keys with non-empty values and the same placeholders.</summary>
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
        foreach (var key in neutral.Keys)
        {
            Placeholders(localized[key]).Should().Equal(Placeholders(neutral[key]), $"key {key} in {locale} must keep the same placeholders");
        }
    }

    /// <summary>The Designer file exposes every neutral key of the user moderation resource set.</summary>
    [Fact]
    public void UserModerationDesigner_ExposesEveryNeutralKey()
    {
        var neutral = Entries(Path.Combine(ResourcesDir(), "Moderation", "UserModeration.resx"));
        var designer = File.ReadAllText(Path.Combine(ResourcesDir(), "Moderation", "UserModeration.Designer.cs"));

        foreach (var key in neutral.Keys)
        {
            designer.Should().Contain($"\"{key}\"", $"Designer must expose {key}");
        }
    }

    /// <summary>The notification templates for warnings and suspensions exist in the neutral file and every locale.</summary>
    [Fact]
    public void NotificationTemplates_DefineSuspensionAndWarningKeysEverywhere()
    {
        var dir = Path.Combine(ResourcesDir(), "Notifications");
        var files = new[] { "NotificationTemplates.resx" }.Concat(Locales.Select(l => $"NotificationTemplates.{l}.resx"));

        foreach (var file in files)
        {
            var keys = Entries(Path.Combine(dir, file)).Keys;
            foreach (var wanted in NotificationKeys)
            {
                keys.Should().Contain(k => k.Contains(wanted), $"{file} must define a template for {wanted}");
            }
        }
    }
}
