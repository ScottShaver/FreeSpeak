using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Components.Account
{
    /// <summary>
    /// Manages redirects for Identity-related operations in Blazor components.
    /// Provides secure redirect methods that prevent open redirect vulnerabilities
    /// and support status message passing via cookies.
    /// </summary>
    /// <param name="navigationManager">The navigation manager for performing redirects.</param>
    internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
    {
        /// <summary>
        /// The cookie name used to pass status messages between redirects.
        /// </summary>
        public const string StatusCookieName = "Identity.StatusMessage";

        /// <summary>
        /// Cookie builder with secure settings for status message cookies.
        /// Cookies expire after 5 seconds and are HttpOnly to prevent XSS.
        /// </summary>
        private static readonly CookieBuilder StatusCookieBuilder = new()
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromSeconds(5),
        };

        /// <summary>
        /// Redirects to the specified URI with protection against open redirects.
        /// If the URI is not well-formed relative, it is converted to a base-relative path.
        /// </summary>
        /// <param name="uri">The URI to redirect to, or null/empty for root.</param>
        public void RedirectTo(string? uri)
        {
            uri ??= "";

            // Prevent open redirects.
            if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
            {
                uri = navigationManager.ToBaseRelativePath(uri);
            }

            navigationManager.NavigateTo(uri);
        }

        /// <summary>
        /// Redirects to the specified URI with query parameters.
        /// </summary>
        /// <param name="uri">The base URI to redirect to.</param>
        /// <param name="queryParameters">Query parameters to append to the URI.</param>
        public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
        {
            var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
            var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
            RedirectTo(newUri);
        }

        /// <summary>
        /// Redirects to the specified URI and sets a status message cookie.
        /// The status message can be displayed on the target page.
        /// </summary>
        /// <param name="uri">The URI to redirect to.</param>
        /// <param name="message">The status message to pass via cookie.</param>
        /// <param name="context">The HTTP context for setting the cookie.</param>
        public void RedirectToWithStatus(string uri, string message, HttpContext context)
        {
            context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
            RedirectTo(uri);
        }

        /// <summary>
        /// Gets the current page path without query string.
        /// </summary>
        private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

        /// <summary>
        /// Redirects to the current page (refresh).
        /// </summary>
        public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

        /// <summary>
        /// Redirects to the current page with a status message.
        /// </summary>
        /// <param name="message">The status message to display.</param>
        /// <param name="context">The HTTP context for setting the cookie.</param>
        public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
            => RedirectToWithStatus(CurrentPath, message, context);

        /// <summary>
        /// Redirects to the invalid user error page with an appropriate message.
        /// </summary>
        /// <param name="userManager">The user manager for getting the user ID.</param>
        /// <param name="context">The HTTP context for setting the cookie and getting the user.</param>
        public void RedirectToInvalidUser(UserManager<ApplicationUser> userManager, HttpContext context)
            => RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
    }
}
