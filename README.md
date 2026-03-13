# FreeSpeak

A modern social media platform built with Blazor Server and .NET 10, featuring real-time interactions, multi-level commenting, and reaction systems.

## Project Overview

FreeSpeak is a feature-rich social networking application that enables users to:
- Create and share posts with image support
- Engage with content through multiple reaction types (Like, Love, Care, Haha, Wow, Sad, Angry)
- Participate in threaded conversations with nested comments (up to 4 levels deep)
- Create and join groups with dedicated posting and moderation
- View detailed analytics for posts and interactions
- Manage their profile with custom avatars
- Receive real-time notifications for social interactions
- Add friends and manage friend connections

### Technology Stack

- **Framework**: .NET 10 / Blazor Server
- **Database**: PostgreSQL with Entity Framework Core
- **UI**: Razor Components with CSS isolation
- **Testing**: xUnit, bUnit, Moq, FluentAssertions
- **Authentication**: ASP.NET Core Identity

## Configuration

The application uses several custom settings in `appsettings.json` to control behavior and limits. These settings are bound to the `SiteSettings` class and injected throughout the application.

### Application Settings

#### Site Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SiteName` | string | `"FreeSpeak"` | The display name of the site, used in titles and branding throughout the application |

#### Comment System Limits

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxFeedPostCommentDepth` | int | `4` | Maximum nesting depth for comment replies. Prevents infinite nesting by limiting how many levels deep users can reply to comments. After reaching this depth, users can still comment but without additional nesting. |
| `MaxFeedPostDirectCommentCount` | int | `30` | Maximum number of direct (top-level) comments allowed per post. This limit helps manage database growth and UI performance. Does not include nested replies in the count. |

#### Caching Configuration

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Caching:UseRedis` | bool | `false` | Enables Redis distributed caching for multi-server deployments. When `false`, uses in-memory caching suitable for single-server deployments. |
| `Caching:RedisConnectionString` | string | `"localhost:6379,abortConnect=false"` | Redis server connection string. Required when `UseRedis` is `true`. Format: `host:port[,options]` |

**Caching Features:**
- **Distributed Caching**: Optional Redis support for horizontal scaling
- **Friendship Cache**: 80%+ performance improvement for friend list queries (5-minute cache)
- **Cache Stampede Prevention**: Thread-safe per-key locking prevents duplicate database queries
- **Query Optimization**: Compiled queries, AsNoTracking, AsSplitQuery for 10-20% performance gains
- **DTO Projections**: Reduces data transfer and memory usage by selecting only required fields

### Example Configuration

```json
{
  "SiteName": "FreeSpeak",
  "MaxFeedPostCommentDepth": 4,
  "MaxFeedPostDirectCommentCount": 30,
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak;Username=youruser;Password=yourpassword"
  },
  "Caching": {
    "UseRedis": false,
    "RedisConnectionString": "localhost:6379,abortConnect=false"
  }
}
```

**For Production with Redis:**
```json
{
  "Caching": {
    "UseRedis": true,
    "RedisConnectionString": "your-redis-server:6379,password=yourpassword,ssl=true,abortConnect=false"
  }
}
```

### Using Settings in Code

Settings are accessed via dependency injection using `IOptions<SiteSettings>`:

```csharp
@inject IOptions<SiteSettings> SiteSettings

<h1>Welcome to @SiteSettings.Value.SiteName</h1>

@code {
    private bool HasReachedCommentLimit => 
        DirectCommentCount >= SiteSettings.Value.MaxFeedPostDirectCommentCount;
}
```

### Environment-Specific Configuration

You can override settings in environment-specific files:
- `appsettings.Development.json` - Development environment
- `appsettings.Production.json` - Production environment
- `appsettings.Test.json` - Testing environment

Example override for testing:
```json
{
  "MaxFeedPostDirectCommentCount": 100,
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak_Test;Username=testuser;Password=testpass"
  }
}
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL 12 or higher
- Visual Studio 2026 or VS Code with C# Dev Kit

### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/ScottShaver/FreeSpeak.git
   cd FreeSpeak
   ```

2. Update the connection string in `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Port=5432;Database=FreeSpeak;Username=youruser;Password=yourpassword"
   }
   ```

3. Run database migrations:
   ```bash
   dotnet ef database update
   ```

4. Run the application:
   ```bash
   dotnet run --project FreeSpeakWeb
   ```

5. Navigate to `https://localhost:5001` in your browser

## Testing

The project includes comprehensive test coverage:

- **Unit Tests**: `FreeSpeakWeb.Tests` - Service and component logic
- **Integration Tests**: `FreeSpeakWeb.IntegrationTests` - Database and end-to-end scenarios

Run all tests:
```bash
dotnet test
```

## Project Structure

```
FreeSpeak/
├── FreeSpeakWeb/                 # Main Blazor Server application
│   ├── Components/
│   │   ├── Pages/                # Routable pages
│   │   │   ├── Base/            # Base components for code reuse
│   │   │   │   └── PostPageBase.cs # Generic base for post pages
│   │   │   ├── Home.razor       # Main feed page
│   │   │   ├── Groups.razor     # Group feed aggregation
│   │   │   ├── GroupView.razor  # Individual group page
│   │   │   ├── Notifications.razor # Notifications center
│   │   │   └── FriendsList.razor # Friends management
│   │   ├── SocialFeed/          # Feed components
│   │   │   ├── FeedArticle.razor # Regular post display
│   │   │   ├── GroupPostArticle.razor # Group post display
│   │   │   ├── PostDetailModal.razor # Full post view with comments
│   │   │   ├── GroupPostDetailModal.razor # Group post modal
│   │   │   └── MultiLineCommentDisplay.razor # Comment component
│   │   └── Shared/              # Shared components
│   │       └── NotificationComponent.razor # Single notification display
│   ├── Data/                     # Entity models and DbContext
│   │   ├── ApplicationDbContext.cs
│   │   ├── Post.cs              # Regular posts
│   │   ├── GroupPost.cs         # Group posts
│   │   ├── Group.cs             # Group entity
│   │   ├── Comment.cs           # Post comments
│   │   ├── GroupPostComment.cs  # Group post comments
│   │   └── UserNotification.cs
│   ├── Services/                 # Business logic services
│   │   ├── PostService.cs       # Post and comment operations
│   │   ├── GroupPostService.cs  # Group post operations
│   │   ├── GroupService.cs      # Group management
│   │   ├── NotificationService.cs # Notification management
│   │   ├── FriendsService.cs    # Friend relationships
│   │   └── ImageUploadService.cs # Image handling
│   ├── Migrations/              # EF Core database migrations
│   └── appsettings.json         # Configuration
├── FreeSpeakWeb.Tests/          # Unit tests
├── FreeSpeakWeb.IntegrationTests/ # Integration tests
└── docs/                         # Documentation
```

## Key Features

### Notification System

The application includes a comprehensive notification system that keeps users informed of social interactions:

**Notification Types:**
- **Post Reactions**: Notified when someone reacts to your post (shows actual reaction emoji: 👍 ❤️ 😂 😮 😢 😠)
- **Post Comments**: Notified when someone comments on your post
- **Comment Replies**: Notified when someone replies to your comment
- **Comment Reactions**: Notified when someone reacts to your comment
- **Friend Requests**: Notified when someone sends you a friend request
- **Friend Accepted**: Notified when someone accepts your friend request

**Features:**
- Visual notification center with unread indicators
- User avatars with notification type badges
- Click notification to view related post in modal
- Auto-scroll to target comment with highlight animation
- Mark all as read / Clear read notifications
- Relative time display (5m ago, 2h ago, etc.)
- Smart notifications (doesn't notify for your own interactions)

### Social Interactions

**Reactions System:**
- Multiple reaction types with emojis
- Reaction breakdown display (shows top 3 reactions)
- Hover-based reaction picker
- Change or remove reactions

**Comments & Replies:**
- Nested comment threads (configurable depth)
- Direct comment limits per post (configurable)
- Comment reactions
- Rich text support with line breaks
- Auto-scroll to highlighted comments from notifications

**Friend System:**
- Send and accept friend requests
- Friend-only post visibility
- Friends list management

### Groups System

Create and participate in topic-based communities:

**Group Features:**
- Create public or private groups
- Group-specific post feeds
- Member management (join, leave, invite)
- Role-based permissions (Admin, Moderator, Member)
- Group post pinning and bookmarking
- Notification muting per post

**Moderation Tools:**
- Ban/unban members
- Delete posts and comments
- Moderator role assignment

**Technical Implementation:**
- Separate entity model (GroupPost, GroupPostComment, GroupPostLike)
- Cascade delete for data integrity
- Cached counts for performance
- See [docs/GROUP_POST_SYSTEM.md](docs/GROUP_POST_SYSTEM.md) for schema details

### Image & Media

**Upload Features:**
- Drag-and-drop image upload
- Multiple image support per post
- Image gallery view
- Profile picture management
- My Uploads page (images and videos)

**Performance Optimizations:**
- Automatic thumbnail generation
- Image resizing service
- Cached thumbnails for better performance
- Secure file serving via API controller

### Security

**Implemented Protections:**
- Rate limiting (global and per-endpoint)
- Secure file access (authentication required)
- CSRF protection with antiforgery tokens
- File path traversal prevention
- Content type validation
- Size limits on uploads

For detailed security audit results, see:
- [docs/SECURITY_AUDIT_RESULTS.md](docs/SECURITY_AUDIT_RESULTS.md) - XSS and SQL injection audit
- [docs/DOS_DDOS_AUDIT_RESULTS.md](docs/DOS_DDOS_AUDIT_RESULTS.md) - DOS/DDOS vulnerability audit

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.