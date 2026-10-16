using System.Security.Claims;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Middleware
{
    /// <summary>
    /// Cuts off signed-in users who are currently suspended: their sign-in cookie is removed and they are
    /// sent to the lockout page that explains the suspension.
    /// </summary>
    public class SuspensionEnforcementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SuspensionEnforcementMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SuspensionEnforcementMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">Logger.</param>
        public SuspensionEnforcementMiddleware(RequestDelegate next, ILogger<SuspensionEnforcementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Checks authenticated requests for an active suspension and ends the session when one exists.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="moderationService">Service that finds the active suspension.</param>
        /// <param name="signInManager">Sign-in manager used to remove the cookie.</param>
        /// <returns>A task representing the middleware work.</returns>
        public async Task InvokeAsync(HttpContext context, IUserModerationService moderationService, SignInManager<ApplicationUser> signInManager)
        {
            var userId = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

            if (userId == null || IsExempt(context.Request.Path))
            {
                await _next(context);
                return;
            }

            ActiveSuspensionInfo? suspension = null;
            try
            {
                suspension = await moderationService.GetActiveSuspensionAsync(userId, context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not check suspension for user {UserId}", userId);
            }

            if (suspension == null)
            {
                await _next(context);
                return;
            }

            await signInManager.SignOutAsync();
            _logger.LogInformation("Ended the session of suspended user {UserId}", userId);

            if (HttpMethods.IsGet(context.Request.Method))
            {
                var name = Uri.EscapeDataString(context.User.Identity?.Name ?? string.Empty);
                context.Response.Redirect($"/Account/Lockout?username={name}");
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
        }

        /// <summary>
        /// Determines whether a path is skipped: static files, framework assets and the pages a suspended user must still reach.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <returns>True when the path is not checked.</returns>
        internal static bool IsExempt(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.StartsWith("/Account/Lockout", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
                || Path.HasExtension(value);
        }
    }
}
