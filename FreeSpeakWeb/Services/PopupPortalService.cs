using Microsoft.AspNetCore.Components;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service that manages popup portal state and coordinates rendering of popups
/// at the document body level to avoid CSS overflow clipping issues.
/// </summary>
public class PopupPortalService
{
    private static int _instanceCounter = 0;
    private readonly int _instanceId;

    public PopupPortalService()
    {
        _instanceId = Interlocked.Increment(ref _instanceCounter);
    }

    /// <summary>
    /// Event raised when the portal state changes and UI should update.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Gets whether a popup is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the current popup content information.
    /// </summary>
    public PopupPortalContent? CurrentContent { get; private set; }

    /// <summary>
    /// Shows a popup with the specified content, anchored to an element.
    /// </summary>
    /// <param name="content">The render fragment to display in the popup.</param>
    /// <param name="anchorElementId">The ID of the element to anchor the popup to.</param>
    /// <param name="placement">The preferred placement of the popup relative to the anchor.</param>
    /// <param name="onClose">Optional callback when the popup is closed.</param>
    public void ShowPopup(RenderFragment content, string anchorElementId, PopupPlacement placement = PopupPlacement.BottomEnd, Action? onClose = null)
    {
        CurrentContent = new PopupPortalContent
        {
            Content = content,
            AnchorElementId = anchorElementId,
            Placement = placement,
            OnClose = onClose
        };
        IsActive = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Hides the currently active popup.
    /// </summary>
    public void HidePopup()
    {
        if (!IsActive) return;

        var onClose = CurrentContent?.OnClose;
        CurrentContent = null;
        IsActive = false;
        NotifyStateChanged();
        onClose?.Invoke();
    }

    /// <summary>
    /// Checks if a popup is currently showing for the specified anchor element.
    /// </summary>
    /// <param name="anchorElementId">The ID of the anchor element to check.</param>
    /// <returns>True if a popup is active and anchored to the specified element.</returns>
    public bool IsShowingForAnchor(string anchorElementId)
    {
        return IsActive && CurrentContent?.AnchorElementId == anchorElementId;
    }

    /// <summary>
    /// Notifies subscribers that state has changed.
    /// </summary>
    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}

/// <summary>
/// Contains information about popup content to be rendered through the portal.
/// </summary>
public class PopupPortalContent
{
    /// <summary>
    /// The render fragment to display in the popup.
    /// </summary>
    public required RenderFragment Content { get; init; }

    /// <summary>
    /// The ID of the HTML element to anchor the popup to.
    /// </summary>
    public required string AnchorElementId { get; init; }

    /// <summary>
    /// The preferred placement of the popup relative to the anchor element.
    /// </summary>
    public PopupPlacement Placement { get; init; } = PopupPlacement.BottomEnd;

    /// <summary>
    /// Optional callback invoked when the popup is closed.
    /// </summary>
    public Action? OnClose { get; init; }
}

/// <summary>
/// Specifies the placement of a popup relative to its anchor element.
/// </summary>
public enum PopupPlacement
{
    /// <summary>
    /// Position below the anchor, aligned to the end (right in LTR).
    /// </summary>
    BottomEnd,

    /// <summary>
    /// Position below the anchor, aligned to the start (left in LTR).
    /// </summary>
    BottomStart,

    /// <summary>
    /// Position above the anchor, aligned to the end (right in LTR).
    /// </summary>
    TopEnd,

    /// <summary>
    /// Position above the anchor, aligned to the start (left in LTR).
    /// </summary>
    TopStart
}
