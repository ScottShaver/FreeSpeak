using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FreeSpeakWeb.Components.Account
{
    /// <summary>
    /// A server-side AuthenticationStateProvider that periodically revalidates the security stamp
    /// for connected users. This ensures that if a user's security stamp changes (e.g., due to
    /// password change or security-related actions), their session is invalidated.
    /// Revalidation occurs every 30 minutes while an interactive circuit is connected.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="scopeFactory">The service scope factory for creating scopes to access services.</param>
    /// <param name="options">The Identity options for accessing claim types.</param>
    internal sealed class IdentityRevalidatingAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<IdentityOptions> options)
        : RevalidatingServerAuthenticationStateProvider(loggerFactory)
    {
        /// <summary>
        /// Gets the interval between authentication state revalidation checks.
        /// </summary>
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        /// <summary>
        /// Validates the authentication state by checking if the user's security stamp is still valid.
        /// </summary>
        /// <param name="authenticationState">The current authentication state to validate.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>True if the authentication state is still valid, false otherwise.</returns>
        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState, CancellationToken cancellationToken)
        {
            // Get the user manager from a new scope to ensure it fetches fresh data
            await using var scope = scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await ValidateSecurityStampAsync(userManager, authenticationState.User);
        }

        /// <summary>
        /// Validates the user's security stamp against the stored stamp in the database.
        /// </summary>
        /// <param name="userManager">The user manager for accessing user data.</param>
        /// <param name="principal">The claims principal representing the user.</param>
        /// <returns>True if the security stamp matches, false if invalid or user not found.</returns>
        private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null)
            {
                return false;
            }
            else if (!userManager.SupportsUserSecurityStamp)
            {
                return true;
            }
            else
            {
                var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
                var userStamp = await userManager.GetSecurityStampAsync(user);
                return principalStamp == userStamp;
            }
        }
    }
}
