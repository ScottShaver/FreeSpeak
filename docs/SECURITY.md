# Security Implementation Guide

## Overview

FreeSpeak implements comprehensive security measures following OWASP Top 10 guidelines and ASP.NET Core best practices. This document covers all security implementations, configurations, and best practices for developers.

## Security Architecture

### Multi-Layer Protection

```
┌─────────────────────────────────────────────────────────────────┐
│                     Security Architecture                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ Rate Limit  │  │ CSP Headers │  │ Input Sanitization      │  │
│  │ Middleware  │  │ Middleware  │  │ (HtmlSanitizationSvc)   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
│         │               │                      │                 │
│         ▼               ▼                      ▼                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │           Authorization & Authentication Layer               ││
│  │     (ASP.NET Core Identity with Role-Based Access)          ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                   │
│         ▼                    ▼                    ▼              │
│  ┌─────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │
│  │ Secure File │  │ Path Traversal  │  │  Entity Framework   │  │
│  │  Storage    │  │  Protection     │  │  (Parameterized)    │  │
│  └─────────────┘  └─────────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## 1. Rate Limiting & DOS/DDOS Protection

### Global Rate Limiting

**File:** `FreeSpeakWeb/Program.cs`

```csharp
// Global rate limit: 500 requests per minute per user
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
        ?? context.Connection.RemoteIpAddress?.ToString() 
        ?? "anonymous";

    return RateLimitPartition.GetFixedWindowLimiter(userId, _ => 
        new FixedWindowRateLimiterOptions
        {
            PermitLimit = 500,
            Window = TimeSpan.FromMinutes(1)
        });
});
```

### Endpoint-Specific Limits

| Endpoint Type | Limit | Window | Purpose |
|---------------|-------|--------|---------|
| Global | 500 req | 1 min | General protection |
| Login | 5 req | 1 min | Brute force prevention |
| Registration | 3 req | 5 min | Spam prevention |
| File Upload | 30 req | 1 min | Resource protection |
| API calls | 100 req | 1 min | API abuse prevention |

## 2. XSS Attack Protection

### HTML Sanitization Service

**File:** `FreeSpeakWeb/Services/HtmlSanitizationService.cs`

All user-generated content is sanitized before storage and display:

```csharp
public class HtmlSanitizationService : IHtmlSanitizationService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizationService()
    {
        _sanitizer = new HtmlSanitizer();

        // Configure allowed tags
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.Add("b");
        _sanitizer.AllowedTags.Add("i");
        _sanitizer.AllowedTags.Add("u");
        _sanitizer.AllowedTags.Add("br");
        _sanitizer.AllowedTags.Add("p");
        // ... additional safe tags

        // Remove dangerous attributes
        _sanitizer.AllowedAttributes.Remove("onclick");
        _sanitizer.AllowedAttributes.Remove("onerror");
        // ... all event handlers removed
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return _sanitizer.Sanitize(html);
    }
}
```

### Content Security Policy Headers

**File:** `FreeSpeakWeb/Program.cs`

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +  // Blazor requirements
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +  // Prevent clickjacking
        "base-uri 'self'; " +
        "form-action 'self';");

    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    await next();
});
```

## 3. SQL Injection Prevention

### Entity Framework Core (Parameterized Queries)

All database operations use Entity Framework Core with LINQ queries, which automatically parameterize all user input:

```csharp
// SAFE - EF Core automatically parameterizes this query
var post = await context.Posts
    .Include(p => p.Images)
    .FirstOrDefaultAsync(p => p.Id == postId);

// SAFE - User input is parameterized
var users = await context.Users
    .Where(u => u.UserName.Contains(searchTerm))
    .ToListAsync();
```

**Never use:**
- `FromSqlRaw` with string concatenation
- `ExecuteSqlRaw` with user input
- Direct SQL string building

## 4. Secure File Storage

### Files Outside wwwroot

All user-uploaded files are stored outside the web root to prevent direct URL access:

**Before (Vulnerable):**
```
wwwroot/uploads/posts/     <- Publicly accessible!
wwwroot/images/profiles/   <- Publicly accessible!
```

**After (Secure):**
```
AppData/uploads/posts/     <- Not directly accessible
AppData/images/profiles/   <- Requires authentication
```

### SecureFileController

**File:** `FreeSpeakWeb/Controllers/SecureFileController.cs`

All file access goes through authenticated endpoints with permission checking:

```csharp
[Authorize]
[HttpGet("post-image/{userId}/{imageId}/{filename}")]
public async Task<IActionResult> GetPostImage(string userId, string imageId, string filename)
{
    // 1. Input validation
    if (!Guid.TryParse(userId, out _) || !Guid.TryParse(imageId, out _))
        return BadRequest("Invalid parameters");

    // 2. Filename validation (prevent path traversal)
    if (!IsValidFilename(filename))
        return BadRequest("Invalid filename");

    // 3. Path validation
    var fullPath = Path.Combine(_uploadPath, userId, imageId, filename);
    if (!IsPathWithinAllowedDirectory(fullPath, _uploadPath))
        return BadRequest("Invalid path");

    // 4. Permission checking
    var post = await GetPostByImage(imageId);
    if (post == null)
        return NotFound();

    if (!await HasAccessToPost(post, currentUserId))
        return Forbid();

    // 5. Serve file
    return PhysicalFile(fullPath, GetContentType(filename));
}
```

### Path Traversal Protection

```csharp
private static bool IsPathWithinAllowedDirectory(string fullPath, string allowedDirectory)
{
    var resolvedPath = Path.GetFullPath(fullPath);
    var resolvedAllowed = Path.GetFullPath(allowedDirectory);

    return resolvedPath.StartsWith(resolvedAllowed, StringComparison.OrdinalIgnoreCase);
}

private static bool IsValidFilename(string filename)
{
    // Only allow alphanumeric, dash, underscore, and dot
    return Regex.IsMatch(filename, @"^[\w\-\.]+$");
}
```

## 5. Authorization & Access Control

### Post Audience Types

Posts have configurable audience restrictions:

| AudienceType | Access Rule |
|--------------|-------------|
| `Public` | All authenticated users |
| `FriendsOnly` | Author + confirmed friends only |
| `MeOnly` | Author only |

### Permission Checking

```csharp
private async Task<bool> HasAccessToPost(Post post, string currentUserId)
{
    // Author always has access
    if (post.AuthorId == currentUserId)
        return true;

    return post.AudienceType switch
    {
        AudienceType.Public => true,
        AudienceType.FriendsOnly => await _friendsService.AreFriendsAsync(
            post.AuthorId, currentUserId),
        AudienceType.MeOnly => false,
        _ => false
    };
}
```

### Group Permissions

| Role | Permissions |
|------|-------------|
| Creator | Full control, cannot be banned |
| Admin | Moderate content, ban members (except creator) |
| Moderator | Delete content, ban regular members |
| Member | Create posts, comments |
| Banned | No access |

## 6. Authentication Security

### Account Lockout

```csharp
services.Configure<IdentityOptions>(options =>
{
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});
```

### Password Requirements

```csharp
options.Password.RequireDigit = true;
options.Password.RequiredLength = 8;
options.Password.RequireNonAlphanumeric = true;
options.Password.RequireUppercase = true;
options.Password.RequireLowercase = true;
```

### Two-Factor Authentication

Full 2FA support with authenticator apps and recovery codes.

## 7. Audit Logging

All security-relevant actions are logged:

- Authentication events (login, logout, failed attempts)
- Content moderation actions
- User management operations
- Permission changes
- File access attempts

See [AUDIT_LOGGING_SYSTEM.md](AUDIT_LOGGING_SYSTEM.md) for details.

## Security Best Practices for Developers

### Do's

✅ Always use Entity Framework LINQ queries
✅ Sanitize all user input before storage
✅ Check permissions before serving content
✅ Validate file paths and names
✅ Use ASP.NET Core's built-in authorization
✅ Log security-relevant events

### Don'ts

❌ Never use `FromSqlRaw` with user input
❌ Never trust client-side validation alone
❌ Never store files in wwwroot
❌ Never expose internal file paths
❌ Never skip permission checks

## OWASP Top 10 Compliance

| Vulnerability | Status | Implementation |
|---------------|--------|----------------|
| A01: Broken Access Control | ✅ Protected | Role-based auth, permission checks |
| A02: Cryptographic Failures | ✅ Protected | HTTPS, secure password hashing |
| A03: Injection | ✅ Protected | EF Core parameterization |
| A04: Insecure Design | ✅ Protected | Defense in depth |
| A05: Security Misconfiguration | ✅ Protected | Security headers, rate limiting |
| A06: Vulnerable Components | ✅ Protected | Regular package updates |
| A07: Auth Failures | ✅ Protected | Account lockout, 2FA |
| A08: Data Integrity | ✅ Protected | Input validation |
| A09: Logging Failures | ✅ Protected | Audit logging |
| A10: SSRF | ✅ Protected | URL validation |
