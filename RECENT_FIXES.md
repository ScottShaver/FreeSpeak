# Recent Fixes

## Fixed Notification Tab Counts (2025-01-XX)

### Issue
In the notifications page, when switching between the "All" and "Unread" tabs, the count displayed next to the tab name would change incorrectly. The counts were being recalculated based on the current page's loaded notifications rather than the actual total.

### Root Cause
The `totalCount` variable was calculated as `notifications.Count + (hasMore ? 1 : 0)` in the `LoadNotifications()` method, which only represented the current page count. When switching tabs, this would recalculate and show different values.

### Solution
**1. Added `GetTotalCountAsync()` to NotificationService**
- New method to get the total count of all notifications for a user
- Mirrors the existing `GetUnreadCountAsync()` pattern
- Ensures consistent count retrieval from database

**2. Updated Notifications Page**
- Replaced `totalCount` with `allNotificationsCount` field
- Load both counts in `LoadNotifications()` regardless of active tab
- "All" tab now always shows `allNotificationsCount`
- "Unread" tab now always shows `unreadCount`
- Updated `DeleteAllRead()` to refresh both counts
- Updated `HandleDeleteNotification()` to decrement both counts appropriately

### Benefits
- Tab counts remain stable when switching between tabs
- More accurate representation of notification totals
- Better user experience with consistent UI

### Files Modified
- `FreeSpeakWeb\Services\NotificationService.cs`
- `FreeSpeakWeb\Components\Pages\Notifications.razor`

---

## Added Profile Field Length Validation (2025-01-XX)

### Issue
Profile UI fields and services didn't enforce the varchar(75) database column length limits, potentially allowing data truncation or database errors.

### Solution
Added comprehensive validation at both UI and service layers to enforce database constraints.

**UI Changes (Index.razor - Profile Management):**
- Updated `StringLength` validation from 50/100 to 75 for:
  - NameSuffix (50 → 75)
  - City (100 → 75)
  - State (100 → 75)
  - Occupation (100 → 75)
- Added PhoneNumber maxlength of 256 to match database
- Added server-side validation with trimming before save
- Added explicit length checks with user-friendly error messages

**UI Changes (Register.razor):**
- Updated `StringLength` validation from 100 to 75 for:
  - FirstName (100 → 75)
  - LastName (100 → 75)
- Added input trimming before processing
- Added server-side validation with specific error messages

**Service Changes (ProfilePictureService):**
- Added validation to ensure userId is not empty
- Added check to ensure generated URL doesn't exceed 75 characters
- Moved URL generation to beginning of method for early validation
- Added detailed logging for URL length violations

### Benefits
- Prevents data truncation at database level
- Provides clear error messages to users before submission
- Validates at multiple layers (client-side, server-side, service-side)
- Trims whitespace automatically to maximize usable space
- Ensures consistency between UI constraints and database schema

### Files Modified
- `FreeSpeakWeb\Components\Account\Pages\Manage\Index.razor`
- `FreeSpeakWeb\Components\Account\Pages\Register.razor`
- `FreeSpeakWeb\Services\ProfilePictureService.cs`

---

## Converted Profile Fields to varchar(75) (2025-01-XX)

### Issue
User profile fields (FirstName, LastName, NameSuffix, City, State, Occupation, ProfilePictureUrl) were created as `text` columns in PostgreSQL instead of sized `varchar` columns.

### Solution
Created migration `ConvertProfileFieldsToVarchar` to alter the column types from `text` to `character varying(75)`.

**Changes Made:**
- Updated `ApplicationUser.cs` to include `[MaxLength(75)]` attributes on all profile fields
- Created migration to alter columns:
  - `FirstName`: text → varchar(75)
  - `LastName`: text → varchar(75)
  - `NameSuffix`: text → varchar(75)
  - `City`: text → varchar(75)
  - `State`: text → varchar(75)
  - `Occupation`: text → varchar(75)
  - `ProfilePictureUrl`: text → varchar(75)
- Applied migration successfully

**Note:** `PhoneNumber` was already correctly sized as varchar(256) from the initial Identity schema.

### Benefits
- Schema now documents expected maximum field lengths
- Better database consistency
- Validation enforced at database level
- Improved clarity for developers

### Files Modified
- `FreeSpeakWeb\Data\ApplicationUser.cs`
- `FreeSpeakWeb\Migrations\20260311162811_ConvertProfileFieldsToVarchar.cs` (new)

---

## Fixed Navigation to Respect User Display Name Preference (2025-01-XX)

### Issue
The top navigation and left sidebar navigation menus were displaying the raw username instead of respecting the user's display name preference setting.

### Root Cause
The NavMenu component was using `@context.User.Identity?.Name` which always returns the username, instead of using `UserPreferenceService.FormatUserDisplayNameAsync()` to format the display name according to the user's preference.

### Solution
Updated NavMenu.razor to:
- Inject `UserPreferenceService`
- Add `currentUserDisplayName` field to store formatted name
- Call `UserPreferenceService.FormatUserDisplayNameAsync()` during initialization
- Replace all instances of `@context.User.Identity?.Name` with `@currentUserDisplayName`

**Changes Made:**
- Top navigation tooltip now shows formatted display name
- Sidebar navigation link now shows formatted display name
- Display name respects user preference (FirstName LastName, Username, etc.)

### Files Modified
- `FreeSpeakWeb\Components\Layout\NavMenu.razor`

### User Experience
Users now see their preferred name format consistently across:
- Top navigation bar
- Left sidebar menu
- All other parts of the application

---

## Fixed Login Redirect to Always Go to Home Page (2025-01-XX)

### Change
All login methods now redirect users to `/home` instead of using ReturnUrl or defaulting to the public home page.

### Implementation Details

**Login Pages Updated:**
- `Login.razor` - Standard username/password login
- `ExternalLogin.razor` - OAuth/external provider login (2 locations)
- `LoginWith2fa.razor` - Two-factor authentication login
- `LoginWithRecoveryCode.razor` - Recovery code login
- `Register.razor` - New user registration with immediate sign-in

**Previous Behavior:**
- Used `ReturnUrl` query parameter to determine redirect destination
- Could redirect to any page user was trying to access
- Default fallback was "/" (public home page)

**New Behavior:**
- All successful logins redirect to `/home`
- Consistent user experience across all login methods
- Works with PublicHome.razor which already redirects authenticated users to `/home`

### Files Modified
- `FreeSpeakWeb\Components\Account\Pages\Login.razor`
- `FreeSpeakWeb\Components\Account\Pages\ExternalLogin.razor`
- `FreeSpeakWeb\Components\Account\Pages\LoginWith2fa.razor`
- `FreeSpeakWeb\Components\Account\Pages\LoginWithRecoveryCode.razor`
- `FreeSpeakWeb\Components\Account\Pages\Register.razor`

---

## Added Individual Notification Deletion (2025-01-XX)

### Feature
Users can now delete individual notifications and all read notifications, with automatic badge count updates.

### Implementation Details

**NotificationComponent:**
- Added delete button with trash icon
- Delete button appears on hover with smooth transition
- Includes `@onclick:stopPropagation` to prevent triggering notification click
- Visual feedback: button turns red on hover and scales on click

**Notifications Page:**
- Added `HandleDeleteNotification` method for individual deletion
- Updated `DeleteAllRead` to refresh badge service
- Updated `MarkAllAsRead` to refresh badge service
- All operations update the badge count in navigation menus

**User Experience:**
- Hover over notification: delete button fades in on the right
- Click delete button: removes notification from list and updates badges
- "Clear read" button: removes all read notifications and updates badges
- Smooth transitions and immediate UI updates

**Badge Integration:**
- All delete operations call `NotificationBadgeService.RefreshUnreadCountAsync()`
- Navigation badges update in real-time when notifications are deleted
- Unread count decreases when unread notifications are deleted

### Files Modified
- `FreeSpeakWeb\Components\Shared\NotificationComponent.razor`
- `FreeSpeakWeb\Components\Shared\NotificationComponent.razor.css`
- `FreeSpeakWeb\Components\Pages\Notifications.razor`

---

## Enhanced Notification Badge System (2025-01-XX)

### Feature Enhancement
Users can now mark notifications as read without navigating to the associated post by clicking the blue unread indicator dot.

### Implementation Details

**NotificationComponent:**
- Added `OnMarkAsReadOnly` event callback parameter
- Unread indicator now has its own click handler with `@onclick:stopPropagation`
- Visual feedback on hover: dot scales up and changes to green
- Added title attribute for accessibility

**Notifications Page:**
- Added `HandleMarkAsReadOnly` method to handle indicator clicks
- Both click methods now refresh `NotificationBadgeService` to update navigation badges
- Clicking the indicator marks as read without opening the post modal

**User Experience:**
- Hover over blue dot: scales to 1.5x and turns green
- Click blue dot: marks notification as read, updates badge count
- Click notification body: marks as read AND opens the post
- Smooth transitions and visual feedback

### Files Modified
- `FreeSpeakWeb\Components\Shared\NotificationComponent.razor`
- `FreeSpeakWeb\Components\Shared\NotificationComponent.razor.css`
- `FreeSpeakWeb\Components\Pages\Notifications.razor`

---

## Implemented Notification Badge System (2025-01-XX)

### Feature
Added real-time notification badges to the navigation menu that display the count of unread notifications.

### Implementation Details

**NotificationBadgeService:**
- Created a scoped service to manage notification badge state
- Implements auto-polling every 5 minutes (300,000ms)
- Provides event-based notifications when unread count changes
- Includes `ResetTimerAsync()` method to restart the polling cycle

**NavMenu Component:**
- Integrated NotificationBadgeService with event subscription
- Added notification badges to both top navigation and sidebar
- Displays count up to 99 (shows "99+" for higher counts)
- Badges update automatically without page reload

**Styling:**
- Red circular badges (#ff4444 background)
- Positioned on top-right of bell icon in top nav
- Positioned on right side of link in sidebar
- Includes shadow and border for visibility

**Timer Reset:**
- Notifications page resets the 5-minute timer when visited
- Immediately refreshes the count when timer is reset
- Ensures badge stays current after user reviews notifications

### Files Created/Modified
- `FreeSpeakWeb\Services\NotificationBadgeService.cs` (new)
- `FreeSpeakWeb\Components\Layout\NavMenu.razor`
- `FreeSpeakWeb\Components\Layout\NavMenu.razor.css`
- `FreeSpeakWeb\Components\Pages\Notifications.razor`
- `FreeSpeakWeb\Program.cs`

---

## Fixed Double-Render Flash on Public Home Page (2025-01-XX)

### Issue
When users visited the welcome/public home page, posts appeared to load twice. Long posts were initially displayed fully expanded and then re-rendered with the "Click to see full post" message, creating a visual flash.

### Root Cause
The `FeedArticle` component had a race condition in its content truncation logic:
1. Component initially rendered with `isTruncated = false` (no CSS truncation applied)
2. After first render, JavaScript measured the content height
3. JavaScript called back to set `shouldShowExpandButton = true` 
4. This triggered `StateHasChanged()` causing a second render with CSS truncation applied

### Solution
Changed `FeedArticleContent.razor` to always apply the CSS truncation class for non-modal views, regardless of the `IsTruncated` flag:

```razor
<!-- Before -->
<div class="article-content @(IsTruncated && !IsModalView ? "truncated" : "")" @ref="contentElementRef">

<!-- After -->
<div class="article-content @(!IsModalView ? "truncated" : "")" @ref="contentElementRef">
```

This ensures:
- CSS truncation is applied from the first render
- JavaScript still measures to determine if "Click to see full post" button should show
- No visual flash as content is consistently truncated from initial render

### Files Modified
- `FreeSpeakWeb\Components\SocialFeed\FeedArticleContent.razor`

---

# Recent Fixes

## Summary
This document details the recent bug fixes and improvements made to address HttpContext issues, theme consistency, emoji picker positioning, and code cleanup.

## HttpContext NullReferenceException Fix

### Problem
Account/Manage pages were using `@rendermode InteractiveServer`, which caused `HttpContext` to be null during component lifecycle methods like `OnInitializedAsync()`. This resulted in `NullReferenceException` when trying to access `HttpContext.User`.

### Root Cause
In Blazor with interactive render modes:
- HttpContext is only available during server-side rendering (SSR)
- Interactive components run in a SignalR circuit where HttpContext is not available
- `[CascadingParameter] HttpContext` expects the value to be cascaded from a parent, but it wasn't being provided

### Solution
**Removed `@rendermode InteractiveServer` from 13 Account/Manage pages:**
- ChangePassword.razor
- Index.razor
- TwoFactorAuthentication.razor
- ExternalLogins.razor
- DeletePersonalData.razor
- SetPassword.razor
- Disable2fa.razor
- EnableAuthenticator.razor
- GenerateRecoveryCodes.razor
- ResetAuthenticator.razor
- PersonalData.razor
- Preferences.razor
- Email.razor

**Why this works:**
- Pages now use static SSR where HttpContext is naturally available
- Forms still work with Blazor's enhanced navigation and form handling
- This is the recommended .NET 8+ pattern for Identity pages

## Theme Consistency Fix

### Problem
After removing `@rendermode InteractiveServer` from Account/Manage pages, the theme selector component stopped working because it requires interactivity for click handlers.

### Solution
**Per-Component Interactivity Pattern:**
1. Added `@rendermode InteractiveServer` to individual interactive components:
   - `ThemeSelector.razor`
   - `UserPreferencesComponent.razor`

2. Updated `ManageNavMenu.razor`:
   - Removed `@rendermode InteractiveServer` from the menu itself
   - Added `data-enhance-nav="false"` to all NavLinks to force full page reloads
   
**Why this works:**
- Static pages can host interactive "islands"
- Full page reloads ensure theme JavaScript in App.razor executes
- ThemeSelector already uses `AuthenticationStateProvider` instead of HttpContext
- Best of both worlds: static pages with interactive components where needed

## Emoji Picker Positioning Fix

### Problem
The emoji picker was:
1. Being cut off in the Post Details modal when post height was short
2. Appearing behind other feed articles in the feed list
3. Being clipped by parent containers with `overflow: hidden`

### Root Cause
1. **Modal Issue**: Emoji picker used `position: absolute`, trapped by modal's stacking context
2. **Feed Issue**: Parent elements (`.article-actions`, `.article-comments`, `.article-comment-editor`) had `z-index: 2`, creating new stacking contexts that trapped the picker
3. **General**: Even with `position: fixed`, child elements can be trapped by parent stacking contexts

### Solution
**1. Changed Emoji Picker to Fixed Positioning:**
```css
.emoji-picker {
    position: fixed;  /* Changed from absolute */
    z-index: 10001;   /* Increased from 1060 */
}

.emoji-picker-backdrop {
    z-index: 10000;   /* Increased from 998 */
}
```

**2. Added Dynamic Position Calculation:**
```javascript
// MultiLineCommentEditor.razor.js
export function calculateEmojiPickerPosition(buttonElement) {
    const rect = buttonElement.getBoundingClientRect();
    const pickerHeight = 300;
    const spaceAbove = rect.top;
    const spaceBelow = window.innerHeight - rect.bottom;
    
    // Position above if not enough space below
    let top = spaceBelow < pickerHeight && spaceAbove > pickerHeight
        ? rect.top - pickerHeight - 8
        : rect.bottom + 8;
        
    // Keep within screen bounds
    let left = Math.max(16, Math.min(rect.left, window.innerWidth - 320 - 16));
    
    return `left: ${left}px; top: ${top}px;`;
}
```

**3. Removed Z-Index from FeedArticle Sections:**
Removed `z-index: 2` from:
- `.article-actions`
- `.article-comments`
- `.article-comment-editor`

**4. Added Minimum Height to Post Detail Modal:**
```css
.post-detail-modal-container {
    min-height: 600px;
}
```

**Why this works:**
- `position: fixed` positions relative to viewport, not parent
- High z-index (10001) ensures it's above all page content
- Removing parent z-index prevents stacking context traps
- Dynamic positioning ensures picker stays on screen
- Minimum modal height provides space for picker

## Debug Logging Cleanup

### Files Cleaned
1. **App.razor**: Removed 11 console.log statements from theme loading script
2. **Program.cs**: Removed 6 emoji-decorated logger.LogWarning statements
3. **ImageResizingService.cs**: Removed 18 debug log statements
4. **SecureFileController.cs**: Removed 100+ lines including diagnostic endpoint
5. **DataMigrationService.cs**: Removed 8 emoji-decorated log statements

### Result
- ~140+ lines of debug code removed
- Production-ready logging
- Cleaner console output
- Better performance (fewer string allocations)

## UI Improvements

### Removed ThemeSelector from PublicHome
- Removed theme selector from center of non-logged-in home page
- Theme selector still available in top navigation bar
- Cleaner, less cluttered welcome screen

## Testing Considerations

### Unit Tests
No unit tests were broken by these changes because:
- Changes are primarily rendering/interaction mode related
- HttpContext usage in components is consistent with before (just in different render mode)
- Component logic and business logic unchanged

### Manual Testing Checklist
- ✅ Account/Manage pages load without NullReferenceException
- ✅ Theme selector works on all Account/Manage pages
- ✅ Theme persists when navigating between Account/Manage pages
- ✅ Emoji picker displays correctly in feed comment fields
- ✅ Emoji picker displays correctly in Post Details modal
- ✅ Emoji picker positions intelligently based on available space
- ✅ No debug logging appears in browser console
- ✅ No emoji logging appears in server logs
- ✅ PublicHome displays cleanly without theme selector in center

## Technical Details

### Blazor Render Modes
**Static SSR (Server-Side Rendering):**
- Default mode for Blazor components
- Renders on server, sends HTML to browser
- HttpContext available
- No WebSocket connection
- Form posts cause page reload

**InteractiveServer:**
- Establishes SignalR connection
- Real-time updates via WebSocket
- HttpContext NOT available in lifecycle methods
- Stateful component in memory
- No page reload on interaction

### Per-Component Interactivity
Components can specify their own render mode:
```razor
@rendermode InteractiveServer

<!-- Component content -->
```

This allows:
- Static page hosting interactive components
- Each component chooses appropriate mode
- Best performance and developer experience

### CSS Stacking Contexts
These CSS properties create new stacking contexts:
- `position: relative/absolute/fixed` with `z-index`
- `transform` (any value)
- `filter` (any value except none)
- `opacity` (less than 1)
- `will-change`
- `isolation: isolate`

When a stacking context is created:
- Child elements are confined to that context
- `position: fixed` children are still contained
- `z-index` is relative to the context, not global

### Solution: Avoid Creating Stacking Contexts
In feed articles, we removed unnecessary `z-index` values to prevent creating stacking contexts that would trap the emoji picker.

## Emoji Features Improvements (Latest)

### Problem 1: Cursor Position Lost During Emoji Text Replacement
When typing emoji codes like `:smile:`, the auto-replacement feature would move the cursor to the end of the text, disrupting the user's typing flow.

### Solution: JavaScript Cursor Preservation
Added `replaceTextPreserveCursor()` function to all text input components:
- Saves cursor position before replacement
- Updates text value
- Restores cursor position
- Triggers Blazor binding update

**Affected Components:**
- PostCreator.razor.js
- PostEditModal.razor.js
- MultiLineCommentEditor.razor.js

### Problem 2: Emoji Picker Insertion Point
When clicking an emoji from the picker, it would insert at the end of the text instead of at the cursor position.

### Solution: Insert at Cursor Position
Added `insertTextAtCursor()` JavaScript function:
- Gets current cursor position
- Inserts emoji at cursor
- Moves cursor after inserted emoji
- Maintains typing flow

**Applied to:** MultiLineCommentEditor.razor.js

### Problem 3: Inconsistent Emoji Button Positioning
Emoji buttons appeared in different positions across components (top-right, bottom-right, etc.).

### Solution: Standardized Positioning
Moved all emoji buttons to **lower left corner inside textarea**:
- Position: absolute, bottom 4px, left 8px
- Button size: 32×32px
- Icon size: 18×18px
- Consistent across all components

**Affected Components:**
- PostCreator.razor
- PostEditModal.razor
- MultiLineCommentEditor.razor

### Problem 4: Emoji Picker Wrong Popup Location
Emoji picker was appearing in the top-right corner of the browser instead of near the button.

### Solution: Absolute Pixel Positioning
Updated `calculateEmojiPickerPosition()` to use `getBoundingClientRect()`:
- Returns `left: Xpx; top: Ypx;` instead of relative positioning
- Positions below button (or above if no space)
- Ensures picker stays on screen
- Matches approach used in MultiLineCommentEditor

**Affected Files:**
- PostCreator.razor.js
- PostEditModal.razor.js

### Problem 5: "Add More Images" Button Not Working in PostEditModal
Clicking the button had no effect.

### Root Causes & Solutions:
1. **Missing StateHasChanged()** - Added to OpenImageUpload/CloseImageUpload
2. **Z-Index Stacking** - ImageUploadModal (z:1000) was behind PostEditModal (z:1050)
   - Increased ImageUploadModal z-index to 1100/1101
3. **Missing IsVisible Parameter** - Added `IsVisible="true"` when rendering modal
4. **Modal in Modal Issue** - Moved ImageUploadModal to Home.razor level
5. **Broken Data Flow** - Added `@ref` and `AddNewImages()` public method to pass images from Home → PostEditModal
6. **No Preview Display** - Added rendering section for new images using base64 DataUrl

**Complete Fix:**
- Moved ImageUploadModal rendering to Home.razor (same level as PostEditModal)
- Added event callbacks for state communication
- Created public `AddNewImages()` method on PostEditModal
- Added preview display for newly selected images
- Added `RemoveNewImage()` method for removing new images before save

**Result:**
- Button opens modal correctly
- Images display in preview
- Images save and upload successfully
- No z-index conflicts

### Problem 6: PostCreator Button Visibility & UX Issues
After previous fixes, two new UX issues appeared:

**Issue 6a: Buttons Disappear When Clicking Emoji/Audience Buttons**
- Clicking emoji or audience selector caused textarea `onblur` event
- `OnBlur()` immediately collapsed component, hiding Post/Cancel buttons
- Users lost access to buttons after clicking UI controls

**Solution:**
- Added `@onmousedown:preventDefault="true"` to emoji button
- Added `@onmousedown:preventDefault="true"` to audience selector button
- Added 150ms delay to `OnBlur()` to allow button clicks to register
- Made `OnBlur()` async to support delay

**Issue 6b: Scrollbar Showing Too Early**
- Scrollbar appeared even with minimal text (1-2 lines)
- Should only show when content exceeds ~10 lines (200px max-height)

**Solution:**
- Changed scrollbar color to `transparent` by default
- Only shows on hover/focus: `scrollbar-color: #d0d0d0 transparent`
- Webkit browsers: thumb transparent by default, colored on hover/focus
- Clean appearance until actually needed

**Issue 6c: Textarea Regains Focus After Posting**
- After clicking "Post" button, textarea would immediately gain focus
- This re-expanded the component, hiding the buttons that were just used
- Confusing UX - buttons appear then disappear

**Solution:**
- Added `blurTextarea()` JavaScript function
- Called after posting to explicitly blur the textarea
- Prevents automatic refocus
- Component stays collapsed until user clicks textarea again

**Affected Files:**
- PostCreator.razor
- PostCreator.razor.css
- PostCreator.razor.js

## CRITICAL SECURITY FIX: XSS Vulnerability (Latest)

### Problem: Cross-Site Scripting (XSS) Vulnerability
User-generated content was being rendered as raw HTML without sanitization, allowing malicious script injection.

**Attack Vector:**
- User types `<script>alert('XSS')</script>` in post or comment
- Content stored in database as-is
- `FormatContentWithLineBreaks()` adds `<br>` tags but doesn't encode HTML
- `@((MarkupString)content)` renders raw HTML without escaping
- Malicious scripts execute in other users' browsers

**Vulnerable Locations:**
- Post content in Home.razor
- Post content in PublicHome.razor
- Comment text in MultiLineCommentDisplay.razor
- Post content in Notifications.razor

### Solution: Multi-Layer HTML Sanitization

**1. Installed HtmlSanitizer NuGet Package**
- Industry-standard HTML sanitization library
- Used by major applications for XSS protection

**2. Created HtmlSanitizationService**
- Strict whitelist: only allows `<br>`, `<p>`, `<b>`, `<i>`, `<u>`, `<em>`, `<strong>`
- **Zero attributes allowed** (prevents onclick, onload, style, etc.)
- **Zero CSS properties allowed**
- **Zero URL schemes allowed** (prevents javascript:, data:, etc.)

**3. Sanitization Process:**
```csharp
public string SanitizeAndFormatContent(string content)
{
    // 1. HTML encode entire content (escapes <, >, &, etc.)
    var encoded = System.Net.WebUtility.HtmlEncode(content);

    // 2. Replace newlines with <br> tags
    var formatted = encoded.Replace("\r\n", "<br>").Replace("\n", "<br>");

    // 3. Sanitize result (validates only safe tags remain)
    return _sanitizer.Sanitize(formatted);
}
```

**4. Updated All Content Rendering:**
- Home.razor - Post content
- PublicHome.razor - Post content
- MultiLineCommentDisplay.razor - Comment text
- Notifications.razor - Post content in detail modal

### Security Impact:
✅ **XSS attacks blocked** - Scripts cannot execute
✅ **HTML injection blocked** - Malicious markup removed
✅ **Event handlers blocked** - onclick, onload, etc. stripped
✅ **External content blocked** - javascript:, data: URLs prevented
✅ **Safe formatting preserved** - Line breaks still work

**Affected Files:**
- HtmlSanitizationService.cs (new)
- Program.cs
- Home.razor
- PublicHome.razor
- MultiLineCommentDisplay.razor
- Notifications.razor

## Best Practices Applied

1. **Use Static SSR by Default**
   - Only add interactivity where needed
   - Better initial page load performance
   - SEO friendly

2. **Per-Component Interactivity**
   - Add `@rendermode InteractiveServer` only to components that need it
   - Keep page-level rendering static when possible

3. **Avoid Unnecessary Z-Index**
   - Only use when layering is actually needed
   - Be aware of stacking context creation
   - Use high z-index for global overlays (modals, popovers)

4. **Use AuthenticationStateProvider Over HttpContext**
   - Works in both SSR and interactive modes
   - Better suited for Blazor architecture
   - More reliable across render modes

5. **Clean Production Code**
   - Remove debug logging before deployment
   - Use proper logging levels (Error, Warning, Information)
   - Avoid emoji decorations in logs (harder to parse/filter)

## Migration Guide

If you have similar issues in your Blazor application:

### For HttpContext Issues:
1. Check if page uses `@rendermode InteractiveServer`
2. If yes and needs HttpContext, remove the directive
3. For interactive components within static pages, add `@rendermode InteractiveServer` to those specific components
4. Consider using `AuthenticationStateProvider` instead of `HttpContext` where possible

### For Theme/Navigation Issues:
1. Add `data-enhance-nav="false"` to links that need full page reload
2. Ensure theme application JavaScript runs on every navigation
3. Use per-component interactivity for theme selectors

### For Overlay Positioning Issues:
1. Use `position: fixed` for global overlays
2. Use high z-index (10000+)
3. Remove unnecessary z-index from parent containers
4. Implement dynamic positioning based on available space

## References

- [.NET 8 Blazor Render Modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes)
- [CSS Stacking Contexts](https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_positioned_layout/Understanding_z-index/Stacking_context)
- [Blazor Static SSR](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/prerendering-and-integration)
