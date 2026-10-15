using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Components.Shared
{
    /// <summary>
    /// Drop-in replacement for <c>DataAnnotationsValidator</c> that localizes the built-in validation
    /// messages (Required, EmailAddress, StringLength, Compare, Range) and field names using
    /// the <c>ValidationMessages</c> resource. English UI keeps the original attribute messages.
    /// </summary>
    public class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
    {
        private ValidationMessageStore? _store;
        private EditContext? _editContext;

        [CascadingParameter]
        private EditContext? CurrentEditContext { get; set; }

        [Inject]
        private IStringLocalizer<FreeSpeakWeb.Resources.Shared.ValidationMessages> Localizer { get; set; } = default!;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            _editContext = CurrentEditContext ?? throw new InvalidOperationException(
                $"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading EditContext.");
            _store = new ValidationMessageStore(_editContext);
            _editContext.OnValidationRequested += HandleValidationRequested;
            _editContext.OnFieldChanged += HandleFieldChanged;
        }

        private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        {
            _store!.Clear();
            foreach (var property in _editContext!.Model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                ValidateProperty(property, new FieldIdentifier(_editContext.Model, property.Name));
            }
            _editContext.NotifyValidationStateChanged();
        }

        private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            var property = e.FieldIdentifier.Model.GetType().GetProperty(e.FieldIdentifier.FieldName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return;
            }

            _store!.Clear(e.FieldIdentifier);
            ValidateProperty(property, e.FieldIdentifier);
            _editContext!.NotifyValidationStateChanged();
        }

        private void ValidateProperty(PropertyInfo property, FieldIdentifier field)
        {
            if (property.GetIndexParameters().Length > 0)
            {
                return;
            }

            var attributes = property.GetCustomAttributes<ValidationAttribute>(true).ToList();
            if (attributes.Count == 0)
            {
                return;
            }

            var model = field.Model;
            var value = property.GetValue(model);
            var displayName = GetDisplayName(property);
            var context = new ValidationContext(model) { MemberName = property.Name, DisplayName = displayName };

            foreach (var attribute in attributes)
            {
                var result = attribute.GetValidationResult(value, context);
                if (result == ValidationResult.Success)
                {
                    continue;
                }

                _store!.Add(field, GetMessage(attribute, property, result?.ErrorMessage));
            }
        }

        /// <summary>
        /// Builds the localized message for a failed attribute, falling back to the attribute's own message.
        /// </summary>
        private string GetMessage(ValidationAttribute attribute, PropertyInfo property, string? original)
        {
            var fallback = original ?? attribute.ErrorMessage ?? string.Empty;
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en")
            {
                return fallback;
            }

            var field = Localize(property);
            return attribute switch
            {
                RequiredAttribute => Format("Validation_Required", fallback, field),
                EmailAddressAttribute => Format("Validation_EmailAddress", fallback, field),
                StringLengthAttribute s when s.MinimumLength > 0 =>
                    Format("Validation_StringLengthRange", fallback, field, s.MaximumLength, s.MinimumLength),
                StringLengthAttribute s => Format("Validation_StringLengthMax", fallback, field, s.MaximumLength),
                CompareAttribute c => Format("Validation_Compare", fallback, field,
                    GetOtherProperty(property, c) is { } other ? Localize(other) : c.OtherProperty),
                RangeAttribute r => Format("Validation_Range", fallback, field, r.Minimum, r.Maximum),
                PhoneAttribute => Format("Validation_Phone", fallback, field),
                Validation.DateOfBirthAttribute => Format("Validation_DateOfBirth", fallback, field,
                    Validation.DateOfBirthAttribute.MinimumYear),
                _ => fallback
            };
        }

        private string Format(string key, string fallback, params object[] args)
        {
            var template = Localizer[key];
            return template.ResourceNotFound ? fallback : string.Format(CultureInfo.CurrentUICulture, template.Value, args);
        }

        private string Localize(PropertyInfo property)
        {
            foreach (var key in GetFieldKeys(property))
            {
                var localized = Localizer["Field_" + key];
                if (!localized.ResourceNotFound)
                {
                    return localized.Value;
                }
            }

            return GetDisplayName(property);
        }

        /// <summary>
        /// Candidate resource key suffixes for a field: the PascalCased display name
        /// (e.g. "New password" becomes "NewPassword") followed by the property name.
        /// </summary>
        private static IEnumerable<string> GetFieldKeys(PropertyInfo property)
        {
            var words = Regex.Split(GetDisplayName(property), "[^A-Za-z0-9]+").Where(w => w.Length > 0);
            var pascal = string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
            if (pascal.Length > 0)
            {
                yield return pascal;
            }

            yield return property.Name;
        }

        private static string GetDisplayName(PropertyInfo property)
        {
            return property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;
        }

        private static PropertyInfo? GetOtherProperty(PropertyInfo property, CompareAttribute compare)
        {
            return property.DeclaringType?.GetProperty(compare.OtherProperty);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_editContext != null)
            {
                _editContext.OnValidationRequested -= HandleValidationRequested;
                _editContext.OnFieldChanged -= HandleFieldChanged;
            }
        }
    }
}
