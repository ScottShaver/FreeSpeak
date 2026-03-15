using FreeSpeakWeb.Components.Account.Pages;
using FreeSpeakWeb.Components.Account.Pages.Manage;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;

namespace Microsoft.AspNetCore.Routing
{
    /// <summary>
    /// Extension methods for mapping Identity-related endpoints required by Blazor Identity components.
    /// Provides endpoints for external login, logout, passkey authentication, and account management.
    /// </summary>
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps additional Identity endpoints required by the Blazor Identity Razor components.
        /// These endpoints handle external authentication, logout, passkey creation/request, and external login linking.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder to add routes to.</param>
        /// <returns>An endpoint convention builder for further configuration.</returns>
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            accountGroup.MapPost("/Logout", async (
                HttpContext context,
                ClaimsPrincipal user,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] IAuditLogRepository auditLogRepository,
                [FromForm] string returnUrl) =>
            {
                var appUser = await userManager.GetUserAsync(user);
                if (appUser != null)
                {
                    // Log the logout to audit log before signing out
                    await auditLogRepository.LogActionAsync(appUser.Id, ActionCategory.UserLogout, new UserLogoutDetails
                    {
                        LogoutMethod = "Manual",
                        IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                    });
                }

                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            accountGroup.MapPost("/PasskeyCreationOptions", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] IAntiforgery antiforgery) =>
            {
                await antiforgery.ValidateRequestAsync(context);

                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";
                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
                {
                    Id = userId,
                    Name = userName,
                    DisplayName = userName
                });
                return TypedResults.Content(optionsJson, contentType: "application/json");
            });

            accountGroup.MapPost("/PasskeyRequestOptions", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] IAntiforgery antiforgery,
                [FromQuery] string? username) =>
            {
                await antiforgery.ValidateRequestAsync(context);

                var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
                var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
                return TypedResults.Content(optionsJson, contentType: "application/json");
            });

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider,
                [FromServices] IAuditLogRepository auditLogRepository) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Log the personal data download to audit log
                await auditLogRepository.LogActionAsync(userId, ActionCategory.UserPersonalData, new UserPersonalDataDetails
                {
                    OperationType = OperationTypeEnum.Download.ToString(),
                    DataScope = "All",
                    Success = true,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                });
                
                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }
    }
}
