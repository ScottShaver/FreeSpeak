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

            // DOS PROTECTION: Configure Blazor Server Circuit options
            builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
            {
                // Limit the number of unacknowledged render batches
                options.MaxBufferedUnacknowledgedRenderBatches = 10;

                // Disconnect circuits after 3 minutes of inactivity
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);

                // Max number of JavaScript interop calls in the queue
                options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
            });

            // DOS PROTECTION: Configure SignalR Hub options
            builder.Services.AddSignalR(options =>
            {
                // Limit maximum message size to 1MB
                options.MaximumReceiveMessageSize = 1 * 1024 * 1024; // 1MB

                // Limit parallel invocations per connection
                options.MaximumParallelInvocationsPerClient = 1;

                // Client timeout settings
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            });

            // Add controllers for API endpoints (including SecureFileController)
            builder.Services.AddControllers();

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

            // Add ImageUploadService
            builder.Services.AddScoped<ImageUploadService>();

            // Add NavigationStateService
            builder.Services.AddScoped<NavigationStateService>();

            // Add NavigationGuardService to prevent logged-in users from accessing auth pages
            builder.Services.AddScoped<NavigationGuardService>();

            // Add DataMigrationService for data migrations
            builder.Services.AddScoped<DataMigrationService>();

            // Add ImageResizingService for performance optimization
            builder.Services.AddSingleton<ImageResizingService>();

            // Add NotificationService
            builder.Services.AddScoped<NotificationService>();

            // Add NotificationBadgeService for badge polling and management
            builder.Services.AddScoped<NotificationBadgeService>();

            // Add NotificationCleanupService as a background service for periodic cleanup
            builder.Services.AddHostedService<NotificationCleanupService>();

            // Add ThemeService for color scheme management
            builder.Services.AddScoped<ThemeService>();

            // Add UserPreferenceService for managing user preferences
            builder.Services.AddScoped<UserPreferenceService>();

            // Add HtmlSanitizationService for XSS protection
            builder.Services.AddSingleton<HtmlSanitizationService>();

            // Add AlertService for user notifications
            builder.Services.AddScoped<AlertService>();

            // Add Group services
            builder.Services.AddScoped<GroupService>();
            builder.Services.AddScoped<GroupRuleService>();
            builder.Services.AddScoped<GroupMemberService>();
            builder.Services.AddScoped<GroupPostService>();
            builder.Services.AddScoped<PinnedGroupPostService>();
            builder.Services.AddScoped<GroupBannedMemberService>();
            builder.Services.AddScoped<GroupPostEventHandlers>();

            // Add helper services for shared functionality
            builder.Services.AddScoped<PostNotificationHelper>();
            builder.Services.AddScoped<GroupAccessValidator>();

            // SECURITY: Add rate limiting to prevent abuse
            builder.Services.AddRateLimiter(options =>
            {
                // File download rate limit: 100 requests per minute per user
                options.AddPolicy("file-download", context =>
                    System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                            QueueLimit = 10
                        }));

                // Global rate limit: 500 requests per minute per user for all endpoints
                options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
                {
                    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                    return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(userId, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                        QueueLimit = 20
                    });
                });

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
                };
            });

            // Configure Kestrel to listen on all network interfaces for mobile testing
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // DOS PROTECTION: Limit maximum request body size to 100MB
                serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB

                // DOS PROTECTION: Limit max concurrent connections
                serverOptions.Limits.MaxConcurrentConnections = 1000;
                serverOptions.Limits.MaxConcurrentUpgradedConnections = 1000;

                // DOS PROTECTION: Request header limits
                serverOptions.Limits.MaxRequestHeaderCount = 100;
                serverOptions.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB

                // DOS PROTECTION: Connection timeout settings
                serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

                serverOptions.ListenAnyIP(5000); // HTTP
                serverOptions.ListenAnyIP(7025, listenOptions =>
                {
                    listenOptions.UseHttps(); // HTTPS
                });
            });

            var app = builder.Build();

            // Seed test users and migrate data in development environment
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var services = scope.ServiceProvider;
                try
                {
                    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                    var dbContext = services.GetRequiredService<ApplicationDbContext>();
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    await DatabaseSeeder.SeedTestUsersAsync(userManager, dbContext, logger);

                    // Migrate existing URLs to secure format
                    var dataMigrationService = services.GetRequiredService<DataMigrationService>();
                    await dataMigrationService.MigrateProfilePictureUrlsAsync();
                    await dataMigrationService.MigratePostImageUrlsAsync();
                    await dataMigrationService.MoveFilesOutOfWwwrootAsync();
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

            // SECURITY: Add Content Security Policy headers for defense-in-depth XSS protection
            app.Use(async (context, next) =>
            {
                // Build CSP connect-src directive based on environment
                var connectSrc = "connect-src 'self' ws: wss:";
                if (app.Environment.IsDevelopment())
                {
                    // In development, allow Browser Link and other localhost debugging tools
                    connectSrc += " http://localhost:* https://localhost:*";
                }

                // CSP header to restrict resource loading and prevent XSS attacks
                context.Response.Headers.Append("Content-Security-Policy", 
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Blazor requires unsafe-inline and unsafe-eval
                    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +  // Allow Bootstrap Icons from CDN
                    "img-src 'self' data: blob:; " +  // Allow data URIs for inline images
                    "font-src 'self' https://cdn.jsdelivr.net; " +  // Allow fonts from CDN
                    $"{connectSrc}; " +  // Allow WebSocket connections for Blazor Server SignalR (and localhost in dev)
                    "frame-ancestors 'none'; " +  // Prevent clickjacking
                    "base-uri 'self'; " +  // Restrict base tag
                    "form-action 'self';");  // Restrict form submissions

                // Additional security headers
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

                await next();
            });

            // SECURITY: Enable rate limiting
            app.UseRateLimiter();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<FreeSpeakWeb.Components.App>()
                .AddInteractiveServerRenderMode();

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            // Map API controllers (including SecureFileController for authenticated file access)
            app.MapControllers();

            app.Run();
        }
    }
}
