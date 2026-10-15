using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// Test localizer that returns the resource key as the localized value.
    /// </summary>
    /// <typeparam name="T">The resource type being localized.</typeparam>
    public class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        /// <inheritdoc />
        public LocalizedString this[string name] => new(name, name);

        /// <inheritdoc />
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        /// <inheritdoc />
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }
}
