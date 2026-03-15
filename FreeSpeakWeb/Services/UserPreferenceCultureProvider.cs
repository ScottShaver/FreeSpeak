using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Localization;
using System.Security.Claims;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Custom culture provider that reads the user's language and country preferences
    /// from the database and applies them to the request culture.
    /// This provider integrates the user preference system with ASP.NET Core localization.
    /// </summary>
    public class UserPreferenceCultureProvider : RequestCultureProvider
    {
        /// <summary>
        /// Determines the culture for the request based on user preferences.
        /// Reads the Language and Country preferences from the database and combines them
        /// to create a culture identifier (e.g., "en-US", "es-ES").
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

                // Get language and country preferences
                var language = await userPreferenceService.GetPreferenceAsync(userId, PreferenceType.Language);
                var country = await userPreferenceService.GetPreferenceAsync(userId, PreferenceType.Country);

                // Combine language and country to create culture identifier (e.g., "en-US")
                var cultureIdentifier = $"{language}-{country}";

                // Validate that the culture is supported
                try
                {
                    var culture = new System.Globalization.CultureInfo(cultureIdentifier);
                    return new ProviderCultureResult(cultureIdentifier, cultureIdentifier);
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    // If the combination is not valid, try language only
                    return new ProviderCultureResult(language, language);
                }
            }
            catch (Exception)
            {
                // If any error occurs, return null to fall back to other providers
                return null;
            }
        }
    }
}
