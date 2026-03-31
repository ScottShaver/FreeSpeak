/**
 * Positions a popup portal element relative to an anchor element.
 * Handles viewport edge detection and automatic flipping when necessary.
 * 
 * @param {HTMLElement} portalElement - The popup portal container element
 * @param {string} anchorElementId - The ID of the anchor element to position relative to
 * @param {string} preferredPlacement - Preferred placement: 'bottom-end', 'bottom-start', 'top-end', 'top-start'
 */
export function positionPopup(portalElement, anchorElementId, preferredPlacement) {
    if (!portalElement || !anchorElementId) {
        console.warn('PopupPortal: Missing portal element or anchor ID');
        return;
    }

    const anchorElement = document.getElementById(anchorElementId);
    if (!anchorElement) {
        console.warn(`PopupPortal: Anchor element with ID '${anchorElementId}' not found`);
        return;
    }

    // Get anchor position relative to viewport (getBoundingClientRect returns viewport-relative coords)
    const anchorRect = anchorElement.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Parse placement
    let [vertical, horizontal] = preferredPlacement.split('-');

    // For position:fixed elements, we use viewport-relative coordinates (no scroll offset needed)
    let top, left;

    // First, render the menu to get its dimensions
    // Reset position temporarily to measure
    portalElement.style.top = '0';
    portalElement.style.left = '0';
    portalElement.style.right = 'auto';
    const portalRect = portalElement.getBoundingClientRect();

    // Vertical positioning
    if (vertical === 'bottom') {
        top = anchorRect.bottom;
        // Check if popup would overflow bottom of viewport
        if (anchorRect.bottom + portalRect.height > viewportHeight) {
            // Flip to top if there's more room above
            if (anchorRect.top > portalRect.height) {
                top = anchorRect.top - portalRect.height;
            }
        }
    } else { // top
        top = anchorRect.top - portalRect.height;
        // Check if popup would overflow top of viewport
        if (top < 0) {
            // Flip to bottom if there's more room below
            if (viewportHeight - anchorRect.bottom > portalRect.height) {
                top = anchorRect.bottom;
            }
        }
    }

    // Horizontal positioning
    if (horizontal === 'end') {
        // Align right edge of popup with right edge of anchor
        left = anchorRect.right - portalRect.width;
        // Check if popup would overflow left of viewport
        if (left < 0) {
            // Flip to start alignment
            left = anchorRect.left;
        }
    } else { // start
        // Align left edge of popup with left edge of anchor
        left = anchorRect.left;
        // Check if popup would overflow right of viewport
        if (left + portalRect.width > viewportWidth) {
            // Flip to end alignment
            left = anchorRect.right - portalRect.width;
        }
    }

    // Ensure popup stays within viewport bounds (with small margin)
    const margin = 8;
    left = Math.max(margin, Math.min(left, viewportWidth - portalRect.width - margin));
    top = Math.max(margin, Math.min(top, viewportHeight - portalRect.height - margin));

    // Apply position (for fixed positioning, no scroll offset needed)
    portalElement.style.top = `${top}px`;
    portalElement.style.left = `${left}px`;
    portalElement.style.right = 'auto'; // Clear the CSS default
}
