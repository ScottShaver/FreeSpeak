using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service to prevent logged-in users from accessing authentication-related pages
    /// </summary>
    public class NavigationGuardService : IDisposable
    {
        private readonly NavigationManager _navigationManager;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly ILogger<NavigationGuardService> _logger;

        // URLs that logged-in users should not be able to access
        private readonly HashSet<string> _restrictedWhenAuthenticatedUrls = new(StringComparer.OrdinalIgnoreCase)
        {
            "/Account/Register",
            "/Account/Login",
            "/Account/ForgotPassword",
            "/Account/ResendEmailConfirmation"
        };

        public NavigationGuardService(
            NavigationManager navigationManager,
            AuthenticationStateProvider authenticationStateProvider,
            ILogger<NavigationGuardService> logger)
        {
            _navigationManager = navigationManager;
            _authenticationStateProvider = authenticationStateProvider;
            _logger = logger;

            // Subscribe to navigation events
            _navigationManager.LocationChanged += OnLocationChanged;
        }

        /// <summary>
        /// Check if the current URL is restricted for logged-in users
        /// </summary>
        public bool IsRestrictedWhenAuthenticated(string url)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            return _restrictedWhenAuthenticatedUrls.Any(restricted => 
                path.Equals(restricted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if user is trying to access a restricted page and redirect if needed
        /// </summary>
        public async Task<bool> ShouldRedirectAuthenticatedUserAsync()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var isAuthenticated = authState.User.Identity?.IsAuthenticated ?? false;

            if (!isAuthenticated)
            {
                return false;
            }

            var currentUrl = _navigationManager.Uri;
            if (IsRestrictedWhenAuthenticated(currentUrl))
            {
                _logger.LogInformation("Authenticated user attempted to access {Url}, redirecting to home", currentUrl);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Redirect authenticated user to home page
        /// </summary>
        public void RedirectToHome()
        {
            _navigationManager.NavigateTo("/", forceLoad: false);
        }

        private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            // Check if user should be redirected
            if (await ShouldRedirectAuthenticatedUserAsync())
            {
                RedirectToHome();
            }
        }

        public void Dispose()
        {
            _navigationManager.LocationChanged -= OnLocationChanged;
        }
    }
}
