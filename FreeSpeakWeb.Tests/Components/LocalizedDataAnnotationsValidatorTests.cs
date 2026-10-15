using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bunit;
using FreeSpeakWeb.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// Tests that <see cref="LocalizedDataAnnotationsValidator"/> localizes validation messages.
    /// </summary>
    public class LocalizedDataAnnotationsValidatorTests
    {
        private class Model
        {
            [Required]
            [Display(Name = "First Name")]
            public string? FirstName { get; set; }

            [StringLength(5, MinimumLength = 2)]
            public string? Code { get; set; } = "toolongvalue";
        }

        private static IReadOnlyList<string> Validate(string culture)
        {
            var original = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            try
            {
                using var ctx = new Bunit.TestContext();
                ctx.Services.AddSingleton<IStringLocalizer<FreeSpeakWeb.Resources.Shared.ValidationMessages>>(
                    new ResourceManagerStringLocalizerFactory(
                        Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance)
                    .Create(typeof(FreeSpeakWeb.Resources.Shared.ValidationMessages)) is { } l
                        ? new TypedLocalizer(l)
                        : throw new InvalidOperationException());

                var editContext = new EditContext(new Model());
                ctx.Render<CascadingValue<EditContext>>(p => p
                    .Add(c => c.Value, editContext)
                    .AddChildContent<LocalizedDataAnnotationsValidator>());
                editContext.Validate();
                return editContext.GetValidationMessages().ToList();
            }
            finally
            {
                CultureInfo.CurrentUICulture = original;
            }
        }

        private class TypedLocalizer : IStringLocalizer<FreeSpeakWeb.Resources.Shared.ValidationMessages>
        {
            private readonly IStringLocalizer _inner;
            public TypedLocalizer(IStringLocalizer inner) => _inner = inner;
            public LocalizedString this[string name] => _inner[name];
            public LocalizedString this[string name, params object[] arguments] => _inner[name, arguments];
            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _inner.GetAllStrings(includeParentCultures);
        }

        [Fact]
        public void SpanishCulture_ReturnsSpanishMessages()
        {
            var messages = Validate("es");

            Assert.Contains("El campo Nombre es obligatorio.", messages);
            Assert.Contains(messages, m => m.Contains("entre 2 y 5 caracteres"));
        }

        [Fact]
        public void EnglishCulture_ReturnsEnglishMessages()
        {
            var messages = Validate("en-US");

            Assert.Contains("The First Name field is required.", messages);
        }
    }
}
