# Changelog

All notable changes to the FreeSpeak project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Per-component interactivity for ThemeSelector and UserPreferencesComponent
- Dynamic emoji picker positioning with JavaScript calculation
- Minimum height (600px) for Post Details modal
- Full page reload navigation for Account/Manage pages to ensure theme consistency
- Cascade delete behavior for nested comment replies
- TEST_FIXES_SUMMARY.md documentation
- JavaScript cursor position preservation during emoji text replacement
- `insertTextAtCursor()` function for emoji picker in MultiLineCommentEditor
- `AddNewImages()` public method in PostEditModal for parent-child image data flow
- Preview display for newly selected images in PostEditModal
- `RemoveNewImage()` method for removing new images before save
- Component tests for PostEditModal (PostEditModalTests.cs)

### Changed
- Account/Manage pages now use static SSR instead of InteractiveServer render mode
- Emoji picker now uses `position: fixed` with z-index 10001 for proper layering
- ManageNavMenu links now include `data-enhance-nav="false"` for full page reloads
- Removed debug logging from App.razor theme loading script
- Removed debug logging from Program.cs migration steps
- Removed debug logging from ImageResizingService
- Removed debug logging from SecureFileController
- Removed debug logging from DataMigrationService
- Removed ThemeSelector from PublicHome.razor center display
- Updated FeedArticleTests to include required PostId and AuthorId parameters
- Updated PostService validation test expectations to match actual error messages
- Updated timestamp tests to use flexible regex patterns instead of exact matches
- Standardized emoji button position to lower left corner across all components (PostCreator, PostEditModal, MultiLineCommentEditor)
- Emoji picker positioning updated to use absolute pixel positioning instead of relative
- ImageUploadModal z-index increased to 1100/1101 (from 1000/1001) to appear above PostEditModal
- ImageUploadModal now rendered at Home.razor level instead of inside PostEditModal

### Fixed
- HttpContext NullReferenceException in Account/Manage pages
- Theme not applying correctly when navigating between Account/Manage pages
- Emoji picker being cut off or hidden behind other elements in feed
- Emoji picker being clipped in Post Details modal
- z-index stacking context issues in FeedArticle preventing emoji picker display
- Comment cascade delete - nested replies are now properly deleted when parent is deleted
- 19 unit test failures (FeedArticle, PostService validation, comment delete, timestamp, ProfilePictureService)
- JSInterop configuration for FeedArticle component tests
- Cursor jumping to end when typing emoji codes like `:smile:` in all text editors
- Emoji picker inserting at end instead of cursor position in MultiLineCommentEditor
- Emoji picker popup appearing in wrong location (top-right corner)
- "Add More Images" button not working in PostEditModal
- Images selected via "Add More Images" not displaying in edit modal preview
- Images selected via "Add More Images" not being uploaded when saving post edits

### Removed
- `@rendermode InteractiveServer` from 13 Account/Manage pages
- `z-index: 2` from `.article-actions`, `.article-comments`, and `.article-comment-editor` CSS
- SecureFileController diagnostic test endpoint
- ~140+ lines of emoji-decorated debug logging across the codebase
- Duplicate console.WriteLine statements from PostEditModal initialization

## [Previous Versions]

### Added - Notification System
- Comprehensive notification system with 8 notification types
- Notification center UI with All/Unread tabs
- Real-time unread notification count
- User avatars with notification type badges in notification list
- Reaction emoji badges for reaction notifications (shows 👍 ❤️ 😂 etc.)
- Click notification to open post in modal
- Auto-scroll to target comment with 2-second blue highlight animation
- Mark all notifications as read functionality
- Clear read notifications functionality
- Bell icon in top navigation bar (for logged-in users)
- Notifications link in left sidebar menu
- Active state highlighting for notifications navigation
- NotificationService with full CRUD operations
- Smart notification logic (doesn't notify for own interactions)
- Reaction breakdown display in PostDetailModal
- Database migration for UserNotifications table
- Comprehensive notification documentation (NOTIFICATIONS.md)

### Changed
- Updated README.md with notification system documentation
- PostService now creates notifications for social interactions
- PostService injected with NotificationService dependency
- Notifications page loads reaction data when opening posts
- PostDetailModal accepts and displays ReactionBreakdown and UserReaction

### Fixed
- Notification type badges now display properly with SVG icons
- Reaction emojis show correctly in notification badges
- PostDetailModal now shows reaction icons next to like count
- Comment highlighting works with data-comment-id attributes

## [1.0.0] - 2024

### Added - Core Features

#### Social Feed System
- Post creation with text and multiple images
- Multi-level nested commenting system (configurable depth)
- Direct comment limits per post (configurable)
- Post detail modal with full comment threads
- Infinite scroll for posts and comments
- Post pinning functionality
- Post deletion with cascade (deletes comments, likes, images)

#### Reaction System
- Multiple reaction types: Like, Love, Care, Haha, Wow, Sad, Angry
- Reaction picker with hover activation
- Reaction breakdown display (shows top 3 reactions)
- Change or remove reactions
- Comment reactions
- Post reactions
- Reaction counts with emoji display

#### Friend System
- Send friend requests
- Accept/decline friend requests
- Friends list view
- Friend-only post visibility
- Friendship management

#### Image & Media Management
- Drag-and-drop image upload
- Multiple image support per post
- Image gallery view with modal
- Profile picture upload and management
- My Uploads page (separate views for images and videos)
- Automatic thumbnail generation
- Image resizing service for performance
- Cached thumbnails for faster loading
- Secure file serving via API controller

#### User Profile
- Custom profile pictures
- Profile picture URL migration to secure endpoints
- User initials fallback for missing profile pictures

### Security Features
- Rate limiting (global and per-endpoint)
  - 100 requests/minute for file downloads
  - 500 requests/minute global limit per user
- Secure file access (authentication required for all user uploads)
- CSRF protection with antiforgery tokens
- File path traversal prevention
- Content type validation for uploads
- File size limits
- Moved uploads outside wwwroot for security
- API-based secure file serving with authorization

### Performance Optimizations
- DbContextFactory for concurrent operations
- Pooled database connections
- Indexed database queries
- Image thumbnail caching
- Automatic image resizing (thumbnail, medium, full sizes)
- Lazy loading for images
- Pagination for feeds and comments

### Developer Experience
- Comprehensive test coverage
  - Unit tests (xUnit, Moq, FluentAssertions)
  - Integration tests with in-memory database
  - Component tests with bUnit
- Configurable site settings (appsettings.json)
- CSS isolation for components
- Clear separation of concerns (Services, Data, Components)
- Diagnostic and troubleshooting documentation

### Documentation
- README.md with setup instructions
- TESTING.md with testing guidelines
- SECURITY_IMPLEMENTATION_SUMMARY.md for security details
- SECURITY_AUDIT_REPORT.md with vulnerability analysis
- IMAGE_SIZE_OPTIMIZATION.md for image handling
- NOTIFICATIONS.md for notification system details
- Multiple diagnostic guides for troubleshooting

### Technical Stack
- .NET 10 with C# 14
- Blazor Server for interactive UI
- PostgreSQL database with Entity Framework Core
- ASP.NET Core Identity for authentication
- Bootstrap 5 for styling
- JavaScript interop for advanced features
- Npgsql for PostgreSQL connectivity

### Migration & Data Management
- Entity Framework Core migrations
- Automatic data migration for URL format changes
- Profile picture URL migration to secure endpoints
- Post image URL migration to secure endpoints
- File system migration (wwwroot to AppData)
- Database seeding for test users in development

---

## Version History

- **[Unreleased]** - Current development version with notification system
- **[1.0.0]** - Initial release with core social features, security, and performance optimizations

---

**Note**: This changelog started being maintained after the notification system implementation. Earlier changes are grouped in the 1.0.0 release.
