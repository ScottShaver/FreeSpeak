using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using System.Security.Claims;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Custom culture provider that reads the user's Culture preference from the database
    /// and applies it to the request culture for localization.
    /// This provider integrates the user preference system with ASP.NET Core localization.
    /// </summary>
    public class UserPreferenceCultureProvider : RequestCultureProvider
    {
        /// <summary>
        /// Determines the culture for the request based on user preferences.
        /// Reads the Culture preference directly from the database.
        /// </summary>
        /// <param name="httpContext">The current HTTP context containing user information.</param>
        /// <returns>
        /// A ProviderCultureResult containing the determined culture, or null if the user
        /// is not authenticated or preferences cannot be determined.
        /// </returns>
        public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            // Only apply for authenticated users
            if (httpContext.User?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            try
            {
                // Get UserPreferenceService from the request scope
                var userPreferenceService = httpContext.RequestServices.GetService<UserPreferenceService>();
                if (userPreferenceService == null)
                {
                    return null;
                }

                // Get culture preference directly - this is now a valid .NET culture identifier
                var culture = await userPreferenceService.GetPreferenceAsync(userId, PreferenceType.Culture);

                // Validate the culture is valid
                if (!string.IsNullOrEmpty(culture) && IsValidCulture(culture))
                {
                    return new ProviderCultureResult(culture, culture);
                }

                return null;
            }
            catch (Exception)
            {
                // If any error occurs, return null to fall back to other providers
                return null;
            }
        }

        /// <summary>
        /// Checks if a culture identifier is valid.
        /// </summary>
        /// <param name="cultureName">The culture identifier to check.</param>
        /// <returns>True if the culture is valid, false otherwise.</returns>
        private static bool IsValidCulture(string cultureName)
        {
            try
            {
                _ = new CultureInfo(cultureName);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
