using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

/// <summary>
/// Verifies that every service localizer uses the marker type matching its own resource file and that the
/// real localization container resolves it to translated (not raw-key) text.
/// </summary>
public class ServiceLocalizerWiringTests
{
    private static readonly Assembly AppAssembly = typeof(FreeSpeakWeb.Services.ServiceLocalizer).Assembly;

    /// <summary>
    /// Supplies every class whose constructor takes an <see cref="IStringLocalizer{T}"/> of a Services/Repositories resource marker.
    /// </summary>
    public static IEnumerable<object[]> LocalizedClasses() =>
        AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("FreeSpeakWeb.Resources") != true)
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(IsServiceMarkerLocalizer)))
            .Select(t => new object[] { t });

    private static bool IsServiceMarkerLocalizer(ParameterInfo p) =>
        p.ParameterType.IsGenericType
        && p.ParameterType.GetGenericTypeDefinition() == typeof(IStringLocalizer<>)
        && (p.ParameterType.GetGenericArguments()[0].Namespace ?? string.Empty).StartsWith("FreeSpeakWeb.Resources.Services")
        || p.ParameterType.IsGenericType
        && p.ParameterType.GetGenericTypeDefinition() == typeof(IStringLocalizer<>)
        && (p.ParameterType.GetGenericArguments()[0].Namespace ?? string.Empty).StartsWith("FreeSpeakWeb.Resources.Repositories");

    /// <summary>
    /// The marker type has the same name as the class that uses it.
    /// </summary>
    /// <param name="type">The service or repository type.</param>
    [Theory]
    [MemberData(nameof(LocalizedClasses))]
    public void Class_UsesOwnMarkerType(Type type)
    {
        var markers = type.GetConstructors().SelectMany(c => c.GetParameters())
            .Where(IsServiceMarkerLocalizer)
            .Select(p => p.ParameterType.GetGenericArguments()[0])
            .Distinct().ToList();

        markers.Should().ContainSingle();
        markers[0].Name.Should().Be(type.Name);
    }

    /// <summary>
    /// The real container localizer returns translated text for every key in en and de (no raw keys).
    /// </summary>
    /// <param name="type">The service or repository type.</param>
    [Theory]
    [MemberData(nameof(LocalizedClasses))]
    public void RealContainerLocalizer_ResolvesAllKeys(Type type)
    {
        var marker = type.GetConstructors().SelectMany(c => c.GetParameters())
            .First(IsServiceMarkerLocalizer).ParameterType.GetGenericArguments()[0];

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        using var provider = services.BuildServiceProvider();
        var localizer = (IStringLocalizer)provider.GetRequiredService(typeof(IStringLocalizer<>).MakeGenericType(marker));

        var original = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "de", "ja" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                var all = localizer.GetAllStrings(includeParentCultures: true).ToList();
                all.Should().NotBeEmpty();
                foreach (var s in all)
                {
                    localizer[s.Name].ResourceNotFound.Should().BeFalse($"{marker.Name}:{s.Name} in {culture}");
                }

                if (culture != "en")
                {
                    var neutral = new CultureInfo("en");
                    var translated = all.Count(s => s.Value != GetEnglish(localizer, s.Name, neutral));
                    translated.Should().BeGreaterThan(0, $"{marker.Name} must have {culture} translations");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static string GetEnglish(IStringLocalizer localizer, string name, CultureInfo culture)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return localizer[name].Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
