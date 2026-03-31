# Portal Pattern Migration Summary

**Branch:** PortalPatternModalRefactor  
**Completed:** June 2025

## Overview

This document summarizes the portal pattern implementation for popup components in the FreeSpeak application. The portal pattern renders popup content at the document body level, preventing CSS `overflow: hidden` clipping issues that occur when popups are rendered inside modal containers.

## Components Migrated to Portal Mode

| Component | Default `UsePortal` | Location |
|-----------|---------------------|----------|
| **PopupMenu** | `true` | `Components/Shared/PopupMenu.razor` |
| **MenuContainer** | `true` (passes to PopupMenu) | `Components/Shared/MenuContainer.razor` |
| **ReactionPicker** | `true` | `Components/SocialFeed/ReactionPicker.razor` |
| **EmojiPicker** | `true` | `Components/Shared/EmojiPicker.razor` |

## Files Modified

### Core Portal Infrastructure
| File | Description |
|------|-------------|
| `Services/PopupPortalService.cs` | Singleton service managing portal state; added `IsShowingForAnchor()` method |
| `Components/Shared/PopupPortal.razor` | Portal host component; removed backdrop to fix mouse event interference |
| `Components/Shared/PopupPortal.razor.css` | Portal container styles; added `pointer-events: none` to backdrop |
| `Components/Shared/PopupPortal.razor.js` | JavaScript for positioning popups relative to anchor elements |

### Popup Components
| File | Description |
|------|-------------|
| `Components/Shared/PopupMenu.razor` | Added `UsePortal` parameter (default: true), portal rendering logic |
| `Components/Shared/MenuContainer.razor` | Wrapper component, passes `UsePortal` to PopupMenu |
| `Components/SocialFeed/ReactionPicker.razor` | Added `UsePortal`, `AnchorElementId`, `IsVisible` parameters; dual render mode |
| `Components/Shared/EmojiPicker.razor` | Added `UsePortal`, `AnchorElementId`, `IsVisible` parameters |

### Component Updates for Portal Support
| File | Description |
|------|-------------|
| `Components/SocialFeed/MultiLineCommentDisplay.razor` | Always uses portal mode for ReactionPicker (consistent with other components) |
| `Components/SocialFeed/ArticleCommentList.razor` | Uses `IsModalView` for "View More Comments" button visibility |
| `Components/SocialFeed/ArticleComponentBase.cs` | Added local handlers for comment reactions to reload internal comments |
| `Components/SocialFeed/UnifiedArticle.razor` | Uses local reaction handlers for proper state updates |
| `Components/SocialFeed/FeedArticleActions.razor` | Uses portal mode for reactions in modals, inline mode in feed |

## Benefits Achieved

### 1. No More Clipping in Modals
- Popup menus, reaction pickers, and emoji pickers now render at the document body level
- Content is no longer clipped by modal containers with `overflow: hidden`

### 2. Consistent Popup Behavior
- All popup components use the same portal infrastructure
- Unified positioning logic via `PopupPortal.razor.js`
- Consistent show/hide behavior across components

### 3. Clean CSS
- No need for `overflow: visible` workarounds on modal containers
- No z-index hacks or positioning tricks
- Modal styling remains clean and maintainable

### 4. Future-Proof Architecture
- New popup components can easily adopt the portal pattern
- `PopupPortalService` provides a clean API for managing popup state
- Flexible: components can opt-out with `UsePortal="false"` when needed

## Components NOT Using Portal Mode

### Intentionally Using Inline Mode
| Component | Reason |
|-----------|--------|
| `FeedArticleActions` (non-modal) | Feed posts don't have clipping issues; uses dual mode based on `IsModalView` |

> **Note:** `MultiLineCommentDisplay` now always uses portal mode (default), making it consistent with other popup components.

## Guidance for Future Popup Components

### When to Use Portal Mode
- Component may appear inside modals or containers with `overflow: hidden`
- Component needs to extend beyond its parent's boundaries
- Component requires consistent positioning across different contexts

### Implementation Pattern

```razor
@inject PopupPortalService PortalService

@if (!UsePortal)
{
    @* Inline rendering *@
    <div class="my-popup">@PopupContent</div>
}
@* Portal mode: visibility managed by portal service via IsVisible parameter *@

@code {
    [Parameter] public bool UsePortal { get; set; } = true;
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string AnchorElementId { get; set; } = string.Empty;

    private bool _previousIsVisible;
    private bool _isShowingPortal;

    protected override void OnParametersSet()
    {
        if (UsePortal && IsVisible != _previousIsVisible)
        {
            if (IsVisible && !_isShowingPortal)
                ShowPortalPopup();
            else if (!IsVisible && _isShowingPortal)
                HidePortalPopup();
        }
        _previousIsVisible = IsVisible;
    }

    private void ShowPortalPopup()
    {
        if (string.IsNullOrEmpty(AnchorElementId)) return;
        if (PortalService.IsShowingForAnchor(AnchorElementId))
        {
            _isShowingPortal = true;
            return;
        }

        _isShowingPortal = true;
        RenderFragment content = builder => { /* Build popup content */ };
        PortalService.ShowPopup(content, AnchorElementId, PopupPlacement.BottomEnd, () =>
        {
            _isShowingPortal = false;
            InvokeAsync(async () => await OnClose.InvokeAsync());
        });
    }

    private void HidePortalPopup()
    {
        if (_isShowingPortal)
        {
            _isShowingPortal = false;
            if (PortalService.IsShowingForAnchor(AnchorElementId))
                PortalService.HidePopup();
        }
    }
}
```

### Key Considerations

1. **Anchor Element ID**: Parent must provide a unique ID for the anchor element
2. **IsVisible Parameter**: Parent controls visibility; portal service manages rendering
3. **Avoid StateHasChanged Loops**: Don't call `StateHasChanged()` in `CancelHideTimer()` or when state hasn't changed
4. **Check Anchor Ownership**: Use `IsShowingForAnchor()` to prevent conflicts between multiple pickers
5. **Dual Mode Support**: Support both portal and inline modes for flexibility

### Parent Component Usage

```razor
<div id="@_popupAnchorId" class="popup-container">
    <button @onclick="TogglePopup">Open</button>

    @* Portal mode (recommended): Always render, portal manages visibility *@
    <MyPopup 
        IsVisible="@showPopup"
        AnchorElementId="@_popupAnchorId"
        OnClose="HidePopup" />
</div>

@code {
    private string _popupAnchorId = $"popup-anchor-{Guid.NewGuid():N}";
    private bool showPopup = false;
}
```

> **Note:** For most cases, always using portal mode is recommended. The `UsePortal="false"` option exists for edge cases where inline rendering is specifically required.

## Known Issues Fixed

1. **Flashing/Flickering**: Fixed by removing full-screen backdrop that intercepted mouse events
2. **Reaction UI Not Updating**: Fixed by adding local handlers that reload internal comments
3. **Multiple Pickers Interfering**: Fixed by tracking anchor ownership in `PopupPortalService`
4. **Unnecessary Re-renders**: Fixed by removing `StateHasChanged()` calls when state unchanged

## Testing Checklist

- [ ] Reaction picker works in feed posts (portal mode)
- [ ] Reaction picker works in post detail modals (portal mode)
- [ ] Comment reaction pickers work at all nesting levels
- [ ] Emoji picker works in comment editors
- [ ] Popup menus work in post headers
- [ ] Popup menus work in modals
- [ ] No flickering or flashing on any picker
- [ ] Reactions persist and UI updates immediately
