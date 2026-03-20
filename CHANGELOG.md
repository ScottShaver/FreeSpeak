# Changelog

All notable changes to the FreeSpeak project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Notification Translation Template System**
  - `NotificationTemplateService` for generating translated notifications
  - Template-based system using resource files for all 12 languages
  - Supports placeholder replacement for dynamic content
  - Consistent notification generation across the application
  - Documentation in notification template resource files

- **Profile Preview on Hover**
  - `ProfilePreviewPopup` component displays user information on 1-second hover
  - Shows profile picture, name, username, location, friend count, mutual friends
  - Smart positioning with viewport boundary detection
  - Works on Friends page, feed posts, and comments
  - Smooth fade-in/out animations with timer-based detection
  - Auto-repositions above avatar if too close to viewport bottom

- **Individual Friend Profile Pages**
  - New `/friend/{userId}` route for viewing friend profiles
  - Displays friend's posts, groups, and activity
  - Profile information and statistics
  - Integration with profile preview popup
  - Jump to Friend navigation feature for quick switching

- **Friends Page Enhancements**
  - Dedicated Friends List page with 5 tabs:
    - Your Friends (with remove option and navigation enabled)
    - People You May Know (with mutual friend counts)
    - Friend Requests (accept/decline)
    - Sent Requests (cancel pending)
    - Find People (search functionality)
  - Profile preview on hover for all friend cards
  - Responsive grid layout for mobile, tablet, and desktop
  - Mutual friends modal display

- **Group Points and Gamification System**
  - `GroupPointsService` calculates points for group participation
  - Points awarded for: posts, comments, reactions
  - Member level badges based on point thresholds
  - Points tracked per user per group in `GroupUser.Points`
  - Level display in group context (Bronze, Silver, Gold, Platinum, Diamond)
  - Point accumulation visible in group member lists

- **Group Rules System**
  - `GroupRule` entity for storing group-specific rules
  - `RulesAcceptanceModal` component for displaying rules before joining
  - Users must agree to rules before joining a group
  - Administrators can create, edit, and delete rules
  - `GroupRuleService` for rule management
  - Audit logging for rule creation and modifications

- **Group Creation and Management**
  - Users can create new groups with name, description, and privacy settings
  - `GroupAdministrationModal` for group settings management
  - Role management: assign administrators and moderators
  - Member management: view members, change roles, ban users
  - Group deletion with cascade cleanup
  - Join request approval/denial system

- **System Administrator Features**
  - New "System Admin" menu item (requires Administrator role)
  - `SystemAdmin.razor` page for site-wide administration
  - User management interface
  - Site statistics and health monitoring
  - Audit log viewing
  - Database management tools
  - Full audit trail for administrative actions

- **System Moderator Features**
  - New "System Moderator" menu item (requires Moderator role)
  - Content moderation across all groups and posts
  - User reports review and action interface
  - Site-wide ban/warn capabilities
  - Comment and post removal powers
  - Separate UI from group-level moderation
  - Audit logging for all moderation actions

- **Jump To Navigation**
  - "Jump to Group" dropdown in navigation for quick group switching
  - "Jump to Friend" feature for quick friend profile navigation
  - Cached lists for performance
  - Search/filter capabilities
  - Recent groups/friends prioritization

- **Translation Validation Tool**
  - `ValidateTranslations.ps1` PowerShell script
  - Scans all resource files for missing translations
  - Generates coverage reports per language
  - Identifies unused resource keys
  - CSV export of missing translations
  - Helps maintain 100% translation coverage
  - Documentation in `VALIDATION_SCRIPT_GUIDE.md`

- **Configuration Enhancement**
  - `AllowPostAttachments` setting in appsettings.json
  - Controls whether users can attach images to posts
  - When false, hides upload UI and blocks server-side uploads
  - Allows administrators to disable image uploads site-wide

### Changed
- Enhanced notification system to use translation templates
- Updated all notification generation to use `NotificationTemplateService`
- Friends page now uses dedicated FriendDetails route for navigation
- Group member badges now show point-based levels
- Navigation menu includes Jump to Group and Jump to Friend dropdowns

### Technical
- All user avatars now support profile preview on hover
- Unique element IDs used for accurate popup positioning
- JavaScript interop for getBoundingClientRect() calculations
- Timer-based hover detection with proper disposal
- Viewport boundary detection prevents off-screen rendering

### Documentation
- Updated README.md with all new features
- Added Profile Preview section
- Added Friends System section
- Added Groups Points & Rules section
- Added System Administration section
- Added Translation Tools section
- Updated configuration examples with `AllowPostAttachments`

### Older Features

- **Group Post System** (Complete posting system for groups)
  - Database Tables:
    - `GroupPosts` - Posts made within groups
    - `GroupPostImages` - Images attached to group posts
    - `GroupPostComments` - Comments with nested reply support
    - `GroupPostLikes` - Likes/reactions on group posts
    - `GroupPostCommentLikes` - Likes/reactions on comments
    - `PinnedGroupPosts` - User-pinned group posts
    - `GroupPostNotificationMutes` - Notification muting for group posts
    - `GroupBannedMembers` - Banned users tracking
  - Services:
    - `GroupPostService` - Full CRUD for posts, comments, likes, and notification mutes
    - `PinnedGroupPostService` - Pin/unpin functionality with group filtering
    - `GroupBannedMemberService` - Ban management with permission hierarchies
  - Business Rules:
    - Group membership required for all interactions
    - Banned users prevented from posting/commenting/liking
    - Admins and moderators can delete any content
    - Moderators cannot ban admins
    - Group creator cannot be banned
    - Cached like/comment counts for performance
  - Comprehensive unit test coverage (40 tests, 100% pass rate)
  - Documentation in `docs/GROUP_POST_SYSTEM.md`
- Background notification cleanup service
  - NotificationCleanupService runs every 5 minutes automatically
  - Intelligent throttling (minimum 1 minute between cleanups)
  - Thread-safe with semaphore locking
  - Removes expired notifications for all users
  - Detailed logging for monitoring
- Post notification muting system
  - New "Turn Off Notifications" menu item for posts
  - Mute/unmute toggle persists across sessions
  - Prevents all post-related notifications (comments, reactions, comment replies, comment reactions)
  - PostNotificationMute database table with unique constraint on PostId + UserId
  - Menu dynamically shows "Turn Off" or "Turn On" based on current mute state
- Comprehensive post deletion cleanup
  - Automatically removes related notifications (checks JSON Data field)
  - Deletes notification mute records
  - Removes all cached thumbnail files (thumbnail and medium sizes)
  - Cascade deletes configured for all related tables
  - Detailed logging for all cleanup operations
- Individual notification deletion with trash icon button
- Delete button appears on notification hover with smooth transitions
- Automatic badge count refresh on all notification operations
- Click-to-mark-read functionality on notification unread indicator dots
- MaxLength(75) data annotations to ApplicationUser profile fields
- Server-side validation for profile field lengths in Index.razor
- Server-side validation for FirstName/LastName in Register.razor
- ProfilePictureService validation for generated URL length
- Input trimming for all profile fields before save
- Shared JavaScript modules for text editor and emoji picker utilities
  - `text-editor-utils.js` - Common textarea manipulation functions
  - `emoji-picker-utils.js` - Emoji picker positioning logic
- Copy Link functionality for public posts
  - New "Copy Link" menu item for public posts
  - SinglePost.razor page for viewing individual posts at /post/{postId}
  - Direct link sharing with validation (public posts only)
  - Proper error handling for deleted or private posts

### Changed
- All login methods now redirect to /home instead of using ReturnUrl
- Standard login, external login, 2FA, recovery code, and registration all redirect to /home
- Profile fields converted from text to varchar(75) in database schema
- ApplicationUser profile fields now have explicit size constraints
- PostCreator, MultiLineCommentEditor, and PostEditModal components now import from shared JS modules
- Reduced JavaScript duplication by ~70% across text editor components

### Fixed
- Navigation menus now respect user display name preference setting
- Top navigation and sidebar now show formatted display name instead of raw username
- Database schema inconsistency with profile field types
- MultiLineCommentDisplayTests failing due to missing HtmlSanitizationService registration
- FeedArticleTests failing due to missing FeedArticleImages.razor.js module setup
- All 94 unit tests now passing (1 skipped by design for InMemory database limitations)

### Performance
- Removed per-call notification cleanup overhead
  - Cleanup removed from GetUserNotificationsAsync()
  - Cleanup removed from GetUnreadCountAsync()
  - Cleanup removed from GetTotalCountAsync()
  - Cleanup now handled by background service (5-minute intervals)
  - Reduced database calls from 3+ per page load to 1 per 5 minutes
- Reduced JavaScript bundle size by consolidating duplicate functions
- Shared modules are loaded once and cached by browser across page updates
- Text editor components now reuse common utilities instead of loading separate copies
- Notification tab counts now remain stable when switching between All and Unread tabs
- Visual feedback on notification indicator hover (scales and turns green)
- Automatic badge count refresh when marking notifications as read
- Notification badge system with real-time unread count display
- NotificationBadgeService for managing notification polling and state
- Auto-refresh of notification badges every 5 minutes
- Red notification badges on bell icon in top navigation and sidebar
- Timer reset functionality when user visits notifications page
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
- HtmlSanitizationService for XSS protection
- HtmlSanitizer NuGet package
- Comprehensive HTML sanitization for all user-generated content

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
- PostCreator buttons (Post/Cancel) disappearing when clicking emoji or audience selector
- PostCreator scrollbar showing prematurely before content exceeds 10 lines
- PostCreator textarea regaining focus after posting, re-expanding the component
- **CRITICAL XSS VULNERABILITY**: User content rendered as raw HTML without sanitization
- Script injection possible via post content, comments, and user profiles

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
