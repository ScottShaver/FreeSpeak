using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Verifies localization of the admin modal components: resource key and placeholder parity across locales,
/// every used key exists, and removed hard-coded English strings do not come back.
/// </summary>
public class AdminModalsLocalizationTests
{
    private static readonly string[] Locales = { "ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh" };

    private static readonly string[] Modals =
    {
        "AuditLogSearchModal", "UserAuditLogModal", "UserRoleManagementModal",
        "UserLockoutManagementModal", "UserCreationModal"
    };

    private static readonly Regex Placeholder = new(@"\{\d+\}", RegexOptions.Compiled);
    private static readonly Regex KeyUsage = new(@"Localizer\[""(\w+)""\]", RegexOptions.Compiled);

    private static string AppDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "FreeSpeakWeb", "Resources", "Admin")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!.FullName, "FreeSpeakWeb");
    }

    private static Dictionary<string, string> Entries(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty);

    private static string ResxPath(string modal, string? locale = null) =>
        Path.Combine(AppDir(), "Resources", "Admin", locale == null ? $"{modal}.resx" : $"{modal}.{locale}.resx");

    /// <summary>
    /// Every locale file has the same keys as the neutral file.
    /// </summary>
    /// <param name="modal">The modal resource name.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(ModalLocaleData))]
    public void LocaleFile_HasSameKeysAsNeutral(string modal, string locale)
    {
        var neutral = Entries(ResxPath(modal));
        var localized = Entries(ResxPath(modal, locale));

        localized.Keys.Should().BeEquivalentTo(neutral.Keys);
    }

    /// <summary>
    /// Every locale value has the same format placeholders as the neutral value and is not empty.
    /// </summary>
    /// <param name="modal">The modal resource name.</param>
    /// <param name="locale">The locale code.</param>
    [Theory]
    [MemberData(nameof(ModalLocaleData))]
    public void LocaleFile_PreservesPlaceholders(string modal, string locale)
    {
        var neutral = Entries(ResxPath(modal));
        var localized = Entries(ResxPath(modal, locale));

        foreach (var (key, value) in neutral)
        {
            var expected = Placeholder.Matches(value).Select(m => m.Value).OrderBy(x => x);
            var actual = Placeholder.Matches(localized[key]).Select(m => m.Value).OrderBy(x => x);
            actual.Should().Equal(expected, $"{modal}.{locale}:{key} must keep the same placeholders");
            localized[key].Should().NotBeNullOrEmpty($"{modal}.{locale}:{key} must be translated");
        }
    }

    /// <summary>
    /// Every key referenced from a modal razor file exists in the neutral resource file and the modal injects its localizer.
    /// </summary>
    /// <param name="modal">The modal component name.</param>
    [Theory]
    [MemberData(nameof(ModalData))]
    public void Razor_UsesOnlyDefinedKeys(string modal)
    {
        var razor = File.ReadAllText(Path.Combine(AppDir(), "Components", "Admin", $"{modal}.razor"));
        var neutral = Entries(ResxPath(modal));

        razor.Should().Contain($"IStringLocalizer<FreeSpeakWeb.Resources.Admin.{modal}>");
        var used = KeyUsage.Matches(razor).Select(m => m.Groups[1].Value).Distinct();
        used.Should().OnlyContain(k => neutral.ContainsKey(k));
    }

    /// <summary>
    /// Previously hard-coded English strings are no longer present in the razor files.
    /// </summary>
    /// <param name="modal">The modal component name.</param>
    /// <param name="text">The removed text.</param>
    [Theory]
    [InlineData("UserLockoutManagementModal", "Failed to load lockout status")]
    [InlineData("UserLockoutManagementModal", "Lockout removed successfully")]
    [InlineData("UserLockoutManagementModal", "MMM dd, yyyy")]
    [InlineData("UserLockoutManagementModal", "Set Lockout Until Date")]
    [InlineData("UserLockoutManagementModal", "return \"Expired\"")]
    [InlineData("UserLockoutManagementModal", "{(durationDays > 1")]
    [InlineData("UserLockoutManagementModal", "alt=\"Profile\"")]
    [InlineData("UserCreationModal", "created successfully!")]
    [InlineData("UserCreationModal", "Enter username")]
    [InlineData("UserCreationModal", "Create New User")]
    [InlineData("UserCreationModal", "An error occurred while creating")]
    [InlineData("UserRoleManagementModal", "Failed to save role changes")]
    [InlineData("UserRoleManagementModal", "alt=\"Profile\"")]
    [InlineData("UserAuditLogModal", "alt=\"Profile\"")]
    [InlineData("UserAuditLogModal", "me-1\"></i>Previous")]
    [InlineData("AuditLogSearchModal", "errorMessage = \"")]
    public void Razor_DoesNotContainHardCodedText(string modal, string text)
    {
        var razor = File.ReadAllText(Path.Combine(AppDir(), "Components", "Admin", $"{modal}.razor"));

        razor.Should().NotContain(text);
    }

    /// <summary>
    /// Supplies every modal and locale combination.
    /// </summary>
    public static IEnumerable<object[]> ModalLocaleData() =>
        Modals.SelectMany(m => Locales.Select(l => new object[] { m, l }));

    /// <summary>
    /// Supplies every modal.
    /// </summary>
    public static IEnumerable<object[]> ModalData() => Modals.Select(m => new object[] { m });
}
