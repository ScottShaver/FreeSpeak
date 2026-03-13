# Configuration Guide

## Overview

FreeSpeak uses `appsettings.json` for configuration with support for environment-specific overrides. Configuration settings are strongly-typed using the Options pattern.

## Configuration Files

### Development
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development environment overrides
- User Secrets - Sensitive development data (connection strings, API keys)

### Production
- `appsettings.json` - Base configuration
- `appsettings.Production.json` - Production environment overrides
- Environment Variables - Sensitive production data

### Testing
- `appsettings.Test.json` - Test environment settings

## Configuration Sections

### 1. Site Settings

Controls application-wide behavior and limits.

```json
{
  "SiteName": "FreeSpeak",
  "MaxFeedPostCommentDepth": 4,
  "MaxFeedPostDirectCommentCount": 30
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SiteName` | string | `"FreeSpeak"` | Display name used throughout the application |
| `MaxFeedPostCommentDepth` | int | `4` | Maximum nesting depth for comment replies |
| `MaxFeedPostDirectCommentCount` | int | `30` | Maximum top-level comments per post |

**Usage in Code:**
```csharp
@inject IOptions<SiteSettings> SiteSettings

<h1>Welcome to @SiteSettings.Value.SiteName</h1>

@code {
    private bool CanAddComment()
    {
        return DirectCommentCount < SiteSettings.Value.MaxFeedPostDirectCommentCount;
    }
}
```

### 2. Database Connection

PostgreSQL connection string configuration.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak;Username=youruser;Password=yourpassword"
  }
}
```

**Connection String Parameters:**
- `Host`: PostgreSQL server hostname
- `Port`: PostgreSQL server port (default: 5432)
- `Database`: Database name
- `Username`: Database user
- `Password`: Database password
- `Pooling`: Enable connection pooling (default: true)
- `Minimum Pool Size`: Minimum connections in pool (default: 0)
- `Maximum Pool Size`: Maximum connections in pool (default: 100)

**Production Example:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-db.example.com;Port=5432;Database=FreeSpeak_Prod;Username=app_user;Password=SecurePassword123;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50;SSL Mode=Require"
  }
}
```

**Using Environment Variables (Recommended for Production):**
```bash
ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=FreeSpeak;..."
```

### 3. Caching Configuration

Controls distributed caching behavior (Redis or in-memory).

```json
{
  "Caching": {
    "UseRedis": false,
    "RedisConnectionString": "localhost:6379,abortConnect=false"
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `UseRedis` | bool | `false` | Enable Redis distributed caching |
| `RedisConnectionString` | string | `localhost:6379` | Redis server connection string |

**Single-Server Deployment (In-Memory):**
```json
{
  "Caching": {
    "UseRedis": false
  }
}
```

**Multi-Server Deployment (Redis):**
```json
{
  "Caching": {
    "UseRedis": true,
    "RedisConnectionString": "redis-server:6379,password=yourpassword,ssl=true,abortConnect=false,connectTimeout=5000,syncTimeout=1000"
  }
}
```

**Redis Connection String Options:**
- `password`: Authentication password
- `ssl`: Enable SSL/TLS (recommended for production)
- `abortConnect`: Set to `false` to allow reconnection on failure
- `connectTimeout`: Connection timeout in milliseconds
- `syncTimeout`: Synchronous operation timeout in milliseconds
- `asyncTimeout`: Asynchronous operation timeout in milliseconds
- `defaultDatabase`: Default Redis database index

**See Also:** [Caching Documentation](CACHING.md)

### 4. Logging Configuration

Controls application logging levels.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  }
}
```

**Log Levels:**
- `Trace`: Very detailed logs (not recommended for production)
- `Debug`: Detailed debugging information
- `Information`: General informational messages
- `Warning`: Warning messages for abnormal events
- `Error`: Error messages for failures
- `Critical`: Critical failure messages
- `None`: Disable logging

**Development Configuration:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "FreeSpeakWeb": "Debug"
    }
  }
}
```

**Production Configuration:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Error",
      "FreeSpeakWeb": "Information"
    }
  }
}
```

### 5. Allowed Hosts

Specifies which hostnames are allowed to access the application.

```json
{
  "AllowedHosts": "*"
}
```

**Development:**
```json
{
  "AllowedHosts": "*"
}
```

**Production (Recommended):**
```json
{
  "AllowedHosts": "freespeak.com;www.freespeak.com"
}
```

## Environment-Specific Configuration

### appsettings.Development.json

Override settings for local development:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak_Dev;Username=devuser;Password=devpass"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "Caching": {
    "UseRedis": false
  }
}
```

### appsettings.Production.json

Override settings for production:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Caching": {
    "UseRedis": true,
    "RedisConnectionString": "redis-cluster.example.com:6379,password=${REDIS_PASSWORD},ssl=true,abortConnect=false"
  }
}
```

**Note:** Never commit sensitive data (passwords, API keys) to source control. Use User Secrets for development and Environment Variables for production.

## User Secrets (Development Only)

Store sensitive development data outside of source control:

### Setup User Secrets

```bash
# Initialize user secrets
dotnet user-secrets init --project FreeSpeakWeb

# Set connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=FreeSpeak;Username=myuser;Password=mypassword" --project FreeSpeakWeb

# Set Redis password
dotnet user-secrets set "Caching:RedisConnectionString" "localhost:6379,password=myredispassword" --project FreeSpeakWeb
```

### View User Secrets

```bash
dotnet user-secrets list --project FreeSpeakWeb
```

## Environment Variables (Production)

Set configuration via environment variables in production:

### Linux/macOS
```bash
export ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=FreeSpeak;..."
export Caching__UseRedis=true
export Caching__RedisConnectionString="redis:6379,password=secret"
```

### Windows PowerShell
```powershell
$env:ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=FreeSpeak;..."
$env:Caching__UseRedis="true"
$env:Caching__RedisConnectionString="redis:6379,password=secret"
```

### Docker Compose
```yaml
services:
  freespeak:
    image: freespeak:latest
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=FreeSpeak;Username=app;Password=${DB_PASSWORD}
      - Caching__UseRedis=true
      - Caching__RedisConnectionString=redis:6379,password=${REDIS_PASSWORD}
```

### Azure App Service
Configure in Application Settings:
- `ConnectionStrings:DefaultConnection`
- `Caching:UseRedis`
- `Caching:RedisConnectionString`

## Accessing Configuration in Code

### Via Options Pattern (Recommended)

**1. Define Settings Class:**
```csharp
public class SiteSettings
{
    public string SiteName { get; set; } = "FreeSpeak";
    public int MaxFeedPostCommentDepth { get; set; } = 4;
    public int MaxFeedPostDirectCommentCount { get; set; } = 30;
}
```

**2. Register in Program.cs:**
```csharp
builder.Services.Configure<SiteSettings>(builder.Configuration);
```

**3. Inject in Services/Components:**
```csharp
public class MyService
{
    private readonly SiteSettings _settings;

    public MyService(IOptions<SiteSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool CanAddComment(int currentCount)
    {
        return currentCount < _settings.MaxFeedPostDirectCommentCount;
    }
}
```

**In Razor Components:**
```razor
@inject IOptions<SiteSettings> SiteSettings

<h1>@SiteSettings.Value.SiteName</h1>
```

### Via IConfiguration

For ad-hoc configuration access:

```csharp
public class MyService
{
    private readonly IConfiguration _configuration;

    public MyService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetSiteName()
    {
        return _configuration["SiteName"] ?? "FreeSpeak";
    }

    public bool UseRedis()
    {
        return _configuration.GetValue<bool>("Caching:UseRedis");
    }
}
```

## Configuration Validation

### Validate on Startup

Add validation to catch configuration errors early:

```csharp
builder.Services.AddOptions<SiteSettings>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**With Data Annotations:**
```csharp
public class SiteSettings
{
    [Required]
    [MinLength(3)]
    public string SiteName { get; set; } = "FreeSpeak";

    [Range(1, 10)]
    public int MaxFeedPostCommentDepth { get; set; } = 4;

    [Range(1, 1000)]
    public int MaxFeedPostDirectCommentCount { get; set; } = 30;
}
```

## Security Configuration

### Kestrel Server Options

Configured in `Program.cs`:

```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Request size limits
    serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB

    // Connection limits
    serverOptions.Limits.MaxConcurrentConnections = 1000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 1000;

    // Header limits
    serverOptions.Limits.MaxRequestHeaderCount = 100;
    serverOptions.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB

    // Timeout settings
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

    // Listen addresses
    serverOptions.ListenAnyIP(5000); // HTTP
    serverOptions.ListenAnyIP(7025, listenOptions =>
    {
        listenOptions.UseHttps(); // HTTPS
    });
});
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    // File download rate limit
    options.AddPolicy("file-download", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? context.Connection.RemoteIpAddress?.ToString() 
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? context.Connection.RemoteIpAddress?.ToString() 
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => 
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 500,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 20
            });
    });
});
```

### Blazor Server Circuit Options

```csharp
builder.Services.Configure<CircuitOptions>(options =>
{
    // Limit unacknowledged render batches
    options.MaxBufferedUnacknowledgedRenderBatches = 10;

    // Disconnect after 3 minutes of inactivity
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);

    // JavaScript interop timeout
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
});
```

### SignalR Hub Options

```csharp
builder.Services.AddSignalR(options =>
{
    // Message size limit
    options.MaximumReceiveMessageSize = 1 * 1024 * 1024; // 1MB

    // Parallel invocations limit
    options.MaximumParallelInvocationsPerClient = 1;

    // Timeout settings
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
```

## Complete Configuration Example

### appsettings.json (Base)
```json
{
  "SiteName": "FreeSpeak",
  "MaxFeedPostCommentDepth": 4,
  "MaxFeedPostDirectCommentCount": 30,
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak;Username=postgres;Password=postgres"
  },
  "Caching": {
    "UseRedis": false,
    "RedisConnectionString": "localhost:6379,abortConnect=false"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Error"
    }
  },
  "Caching": {
    "UseRedis": true
  },
  "AllowedHosts": "freespeak.com;www.freespeak.com"
}
```

### Environment Variables (Production)
```bash
# Database
ConnectionStrings__DefaultConnection="Host=prod-pg.example.com;Port=5432;Database=FreeSpeak_Prod;Username=app_user;Password=${DB_PASSWORD};SSL Mode=Require;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50"

# Redis
Caching__RedisConnectionString="redis-cluster.example.com:6379,password=${REDIS_PASSWORD},ssl=true,abortConnect=false,connectTimeout=5000"

# Application
ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://+:80;https://+:443"
```

## Troubleshooting

### Configuration Not Loading

**Check:**
1. File exists in correct location
2. File is set to "Copy to Output Directory"
3. Environment name matches (`ASPNETCORE_ENVIRONMENT`)
4. JSON syntax is valid

### Environment Variables Not Working

**Format:**
- Use double underscores `__` to represent nesting
- Example: `ConnectionStrings__DefaultConnection` not `ConnectionStrings:DefaultConnection`

### Values Not Overriding

**Configuration Priority (highest to lowest):**
1. Command-line arguments
2. Environment variables
3. User secrets (Development only)
4. appsettings.{Environment}.json
5. appsettings.json

## Related Documentation

- [Caching Strategy](CACHING.md)
- [Performance Optimization](PERFORMANCE_OPTIMIZATION.md)
- [Security Documentation](SECURITY_AUDIT_RESULTS.md)
