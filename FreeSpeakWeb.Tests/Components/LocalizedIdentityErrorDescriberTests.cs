using System.Globalization;
using FluentAssertions;
using FreeSpeakWeb.Components.Account;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// Tests for <see cref="LocalizedIdentityErrorDescriber"/> using the real resource bundles.
    /// </summary>
    public class LocalizedIdentityErrorDescriberTests
    {
        /// <summary>
        /// Creates a describer backed by the real resource files.
        /// </summary>
        private static LocalizedIdentityErrorDescriber CreateDescriber()
        {
            var factory = new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
            var localizer = new StringLocalizer<FreeSpeakWeb.Resources.Account.LocalizedIdentityErrorDescriber>(factory);
            return new LocalizedIdentityErrorDescriber(localizer);
        }

        /// <summary>
        /// Runs an action under a specific UI culture and restores the previous one.
        /// </summary>
        private static T WithCulture<T>(string culture, Func<T> action)
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                return action();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        /// <summary>
        /// English output includes the formatted argument and keeps the Identity error code.
        /// </summary>
        [Fact]
        public void PasswordTooShort_English_FormatsLength()
        {
            var error = WithCulture("en", () => CreateDescriber().PasswordTooShort(6));

            error.Code.Should().Be("PasswordTooShort");
            error.Description.Should().Contain("6");
        }

        /// <summary>
        /// Every <see cref="Microsoft.AspNetCore.Identity.IdentityError"/>-returning member of the base
        /// describer is overridden, keeps its error code and, in English, matches the framework text.
        /// </summary>
        [Fact]
        public void AllMembers_AreOverridden_KeepCodesAndEnglishText()
        {
            var localized = CreateDescriber();
            var baseline = new Microsoft.AspNetCore.Identity.IdentityErrorDescriber();
            var methods = typeof(Microsoft.AspNetCore.Identity.IdentityErrorDescriber)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(Microsoft.AspNetCore.Identity.IdentityError))
                .ToList();

            methods.Should().NotBeEmpty();
            foreach (var method in methods)
            {
                var args = method.GetParameters()
                    .Select(p => p.ParameterType == typeof(string) ? (object)"x" : 7)
                    .ToArray();

                var expected = (Microsoft.AspNetCore.Identity.IdentityError)method.Invoke(baseline, args)!;
                var actual = WithCulture("en", () => (Microsoft.AspNetCore.Identity.IdentityError)method.Invoke(localized, args)!);

                actual.Code.Should().Be(expected.Code, method.Name);
                actual.Description.Should().Be(expected.Description, method.Name);
            }
        }

        /// <summary>
        /// A non-English culture returns a translated description, not the English one.
        /// </summary>
        [Theory]
        [InlineData("de")]
        [InlineData("ja")]
        [InlineData("ar")]
        public void PasswordRequiresDigit_NonEnglish_IsTranslated(string culture)
        {
            var english = WithCulture("en", () => CreateDescriber().PasswordRequiresDigit().Description);
            var translated = WithCulture(culture, () => CreateDescriber().PasswordRequiresDigit().Description);

            translated.Should().NotBeNullOrWhiteSpace().And.NotBe(english);
        }
    }
}
