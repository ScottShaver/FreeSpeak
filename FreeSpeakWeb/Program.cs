using FreeSpeakWeb.Components;
using FreeSpeakWeb.Components.Account;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Configure SiteSettings
            builder.Services.Configure<SiteSettings>(builder.Configuration);

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Register DbContext as scoped service using the factory for Identity and middleware
            builder.Services.AddScoped<ApplicationDbContext>(sp => 
                sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;
                    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            // Add ProfilePictureService
            builder.Services.AddScoped<ProfilePictureService>();

            // Add FriendsService
            builder.Services.AddScoped<FriendsService>();

            // Add PostService
            builder.Services.AddScoped<PostService>();

            // Configure Kestrel to listen on all network interfaces for mobile testing
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(5000); // HTTP
                serverOptions.ListenAnyIP(7025, listenOptions =>
                {
                    listenOptions.UseHttps(); // HTTPS
                });
            });

            var app = builder.Build();

            // Seed test users in development environment
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var services = scope.ServiceProvider;
                try
                {
                    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    await DatabaseSeeder.SeedTestUsersAsync(userManager, logger);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "[{ExceptionType}] An error occurred while seeding the database. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            // Profile picture endpoint - serves images by user ID without exposing file paths
            app.MapGet("/api/profile-picture/{userId}", async (string userId, ProfilePictureService profilePictureService) =>
            {
                var imageBytes = await profilePictureService.GetProfilePictureAsync(userId);

                if (imageBytes == null)
                {
                    return Results.NotFound();
                }

                return Results.File(imageBytes, "image/jpeg", enableRangeProcessing: true);
            })
            .RequireAuthorization();

            app.Run();
        }
    }
}
