using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Resolves the <see cref="IStringLocalizer{T}"/> used by services for user-facing result messages.
    /// Dependency injection always supplies the real localizer; the fallback exists so services can be constructed
    /// without one (for example in unit tests) and still return the neutral (English) resource text.
    /// </summary>
    public static class ServiceLocalizer
    {
        /// <summary>
        /// Returns the supplied localizer, or a resource-backed localizer for <typeparamref name="T"/> when none was supplied.
        /// </summary>
        /// <typeparam name="T">The service type that owns the resource file.</typeparam>
        /// <param name="localizer">The injected localizer, if any.</param>
        /// <returns>A localizer that is never <c>null</c>.</returns>
        public static IStringLocalizer<T> Resolve<T>(IStringLocalizer<T>? localizer)
        {
            return localizer ?? new StringLocalizer<T>(
                new ResourceManagerStringLocalizerFactory(
                    Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
                    NullLoggerFactory.Instance));
        }
    }
}
