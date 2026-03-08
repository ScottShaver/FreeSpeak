# Security Audit Report - FreeSpeak Web Application
Generated: 2025-01-08

## Executive Summary
This report identifies critical security vulnerabilities in the FreeSpeak web application's public endpoints. Several HIGH PRIORITY issues require immediate attention.

---

## 🔴 CRITICAL VULNERABILITIES

### 1. **Insecure Direct File Access via Static Files**
**Severity:** CRITICAL  
**Location:** `wwwroot/uploads/` directory  
**Issue:** The `uploads` folder is inside `wwwroot`, making ALL user-uploaded files publicly accessible without authentication.

**Attack Vector:**
```
GET /uploads/posts/{anyUserId}/images/{anyFilename}.jpg
GET /uploads/posts/{anyUserId}/videos/{anyFilename}.mp4
```
Even though you created the SecureFileController, if files are in `wwwroot/uploads/`, they are **STILL DIRECTLY ACCESSIBLE** via static file middleware!

**Impact:**
- Any user can enumerate and download all uploaded images/videos
- Profile pictures can be accessed without authentication
- Private content marked as "Only Me" or "Friends Only" is publicly accessible
- Potential privacy violations and GDPR issues

**Fix Required:**
1. **IMMEDIATELY** move upload directory OUTSIDE of `wwwroot`
2. Serve ALL files through SecureFileController only

---

### 2. **Missing Authorization Checks in SecureFileController**
**Severity:** HIGH  
**Location:** `FreeSpeakWeb\Controllers\SecureFileController.cs`

**Issues:**

#### 2a. Profile Pictures - No Privacy Validation
```csharp
[HttpGet("profile-picture/{userId}")]
public async Task<IActionResult> GetProfilePicture(string userId)
```
**Problem:** Any authenticated user can view ANY user's profile picture, even if that user blocked them or set their profile to private.

**Attack:** Authenticated user requests `/api/secure-files/profile-picture/{victim-user-id}`

#### 2b. Post Images - No Audience/Permission Validation
```csharp
[HttpGet("post-image/{userId}/{imageId}/{filename}")]
public IActionResult GetPostImage(string userId, string imageId, string filename)
```
**Problem:** 
- No check if the requesting user has permission to view this post
- Doesn't validate `AudienceType` (Public/FriendsOnly/MeOnly)
- Doesn't verify friendship status
- User can enumerate image IDs to download private content

**Attack:** 
```csharp
// Attacker discovers a post image URL, changes the GUID to enumerate other images
GET /api/secure-files/post-image/victim-id/different-guid/stolen.jpg
```

#### 2c. Post Videos - Same Issues as Images
Same vulnerability as post images.

---

### 3. **Path Traversal Vulnerability Still Possible**
**Severity:** MEDIUM  
**Location:** `SecureFileController.GetPostImage()` and `GetPostVideo()`

**Current Protection:**
```csharp
if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
```

**Issues:**
1. Doesn't validate `userId` parameter - could contain path traversal characters
2. Doesn't validate `imageId` parameter
3. URL encoding bypass possible: `%2e%2e`, `%2f`, `%5c`
4. Doesn't verify the final path is within the expected directory

**Attack Vectors:**
```
GET /api/secure-files/post-image/../../../secrets/config.json/x/file.jpg
GET /api/secure-files/post-image/%2e%2e%2f%2e%2e/x/x/sensitive.jpg
```

---

### 4. **No Rate Limiting**
**Severity:** MEDIUM  
**All Endpoints**

**Problem:** No rate limiting on any endpoints allows:
- Brute force attacks on file enumeration
- Denial of Service (DoS) attacks
- Bandwidth exhaustion
- User enumeration attacks

**Attack:** Automated script downloads thousands of files or attempts to enumerate valid user IDs.

---

### 5. **Insufficient Input Validation**
**Severity:** MEDIUM  
**Location:** Multiple endpoints

**Issues:**
- `userId` parameter not validated (could be SQL injection in logs, very long strings, etc.)
- `imageId` parameter not validated (should be GUID format)
- `filename` not validated for allowed characters
- No max length checks on parameters

---

### 6. **Information Disclosure**
**Severity:** LOW-MEDIUM  
**Location:** Error messages and logging

**Issues:**
```csharp
_logger.LogWarning("Post image not found: {FilePath}", filePath);
return StatusCode(500, "An error occurred while retrieving the image");
```

**Problems:**
- File paths logged could expose server structure
- Generic error messages don't help debugging in production
- 500 errors leak that the endpoint exists even for invalid requests

---

## 📋 RECOMMENDATIONS

### Immediate Actions Required

#### 1. Move Upload Directory Outside wwwroot
```csharp
// In ImageUploadService and ProfilePictureService
// CHANGE FROM:
var uploadsPath = Path.Combine(environment.WebRootPath, "uploads", "posts");

// TO:
var uploadsPath = Path.Combine(environment.ContentRootPath, "AppData", "uploads", "posts");
```

#### 2. Add Authorization Logic to SecureFileController
```csharp
[HttpGet("post-image/{userId}/{imageId}/{filename}")]
public async Task<IActionResult> GetPostImage(string userId, string imageId, string filename)
{
    // 1. Validate inputs
    if (!Guid.TryParse(imageId, out _))
        return BadRequest("Invalid image ID");
    
    // 2. Find the post/image in database
    var postImage = await _dbContext.PostImages
        .Include(pi => pi.Post)
        .ThenInclude(p => p.Author)
        .FirstOrDefaultAsync(pi => pi.ImageUrl.Contains(imageId));
    
    if (postImage == null)
        return NotFound();
    
    // 3. Check permissions based on AudienceType
    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    
    if (!await CanUserViewPost(postImage.Post, currentUserId))
        return Forbid();
    
    // 4. Serve the file
    // ... existing file serving code
}

private async Task<bool> CanUserViewPost(Post post, string requestingUserId)
{
    // Post is by the requesting user
    if (post.AuthorId == requestingUserId)
        return true;
    
    // Public posts
    if (post.AudienceType == AudienceType.Public)
        return true;
    
    // MeOnly posts
    if (post.AudienceType == AudienceType.MeOnly)
        return false;
    
    // FriendsOnly posts - check friendship
    if (post.AudienceType == AudienceType.FriendsOnly)
    {
        return await _friendsService.AreFriendsAsync(post.AuthorId, requestingUserId);
    }
    
    return false;
}
```

#### 3. Add Comprehensive Path Validation
```csharp
private bool IsValidFileName(string filename)
{
    // Only allow alphanumeric, dash, underscore, and single dot for extension
    var allowedPattern = @"^[a-zA-Z0-9\-_]+\.[a-zA-Z0-9]+$";
    return Regex.IsMatch(filename, allowedPattern);
}

private bool IsValidUserId(string userId)
{
    // Should be a GUID format
    return Guid.TryParse(userId, out _);
}

private bool IsPathWithinAllowedDirectory(string fullPath, string allowedDirectory)
{
    var fullPathNormalized = Path.GetFullPath(fullPath);
    var allowedDirNormalized = Path.GetFullPath(allowedDirectory);
    return fullPathNormalized.StartsWith(allowedDirNormalized, StringComparison.OrdinalIgnoreCase);
}
```

#### 4. Implement Rate Limiting
```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100, // requests
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });
});

// In middleware
app.UseRateLimiter();

// On SecureFileController
[EnableRateLimiting("file-download")]
public class SecureFileController : ControllerBase
```

#### 5. Add Request Validation Middleware
```csharp
public class SecureFileValidationMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/api/secure-files"))
        {
            // Check for suspicious patterns
            var path = context.Request.Path.ToString();
            
            if (path.Contains("..") || 
                path.Contains("%2e%2e") || 
                path.Contains("%252e") ||
                path.Contains("//") ||
                path.Length > 500)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid request");
                return;
            }
        }
        
        await next(context);
    }
}
```

---

## 🔒 Additional Security Hardening

### 1. Add CORS Policy
Ensure only your domain can access API endpoints.

### 2. Add Content Security Policy (CSP)
Prevent XSS attacks through uploaded files.

### 3. Implement File Type Validation
Validate actual file content, not just extension:
```csharp
private async Task<bool> IsValidImage(Stream stream)
{
    try
    {
        using var image = await Image.LoadAsync(stream);
        return true;
    }
    catch
    {
        return false;
    }
}
```

### 4. Add Virus Scanning
For production, integrate with antivirus API for uploaded files.

### 5. Implement Audit Logging
Log all file access attempts with user ID, IP, timestamp for security monitoring.

### 6. Add Cache Headers
Set appropriate cache headers to prevent sensitive content caching:
```csharp
Response.Headers.Add("Cache-Control", "private, no-store");
Response.Headers.Add("X-Content-Type-Options", "nosniff");
```

---

## Priority Matrix

| Issue | Severity | Effort | Priority |
|-------|----------|--------|----------|
| Files in wwwroot | CRITICAL | Medium | 🔴 FIX NOW |
| Missing authorization checks | HIGH | High | 🔴 FIX NOW |
| Path traversal improvements | MEDIUM | Low | 🟡 Next Sprint |
| Rate limiting | MEDIUM | Low | 🟡 Next Sprint |
| Input validation | MEDIUM | Low | 🟡 Next Sprint |
| Information disclosure | LOW | Low | 🟢 Future |

---

## Compliance Concerns

### GDPR
- Users marked content as "Only Me" but it's publicly accessible
- Right to deletion may not fully remove accessible files

### Data Privacy
- Private messages/content accessible without proper authorization
- User enumeration possible

---

## Testing Recommendations

1. **Penetration Testing**: Hire security firm to test file access
2. **Automated Security Scanning**: Use tools like OWASP ZAP
3. **Code Review**: Have another developer review authorization logic
4. **Manual Testing**: Test with different user roles and permissions

---

## Conclusion

The current implementation has **CRITICAL security flaws** that could lead to:
- Privacy violations
- Data breaches
- Unauthorized access to private content
- Legal liability under GDPR/CCPA

**Immediate action required on items marked 🔴 before production deployment.**
