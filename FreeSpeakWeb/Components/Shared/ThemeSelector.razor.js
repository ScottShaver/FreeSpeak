// ThemeSelector click-outside-to-close functionality

let clickOutsideHandler = null;
let themeSelectorElement = null;

export function setupClickOutside(element, dotNetHelper) {
    // Remove any existing handler first
    cleanupClickOutside();
    
    themeSelectorElement = element;
    
    // Add a small delay to prevent the click that opened the menu from immediately closing it
    setTimeout(() => {
        clickOutsideHandler = (event) => {
            // Check if the click was outside the theme selector element
            if (themeSelectorElement && !themeSelectorElement.contains(event.target)) {
                // Call back to Blazor to close the menu
                dotNetHelper.invokeMethodAsync('CloseMenu');
            }
        };
        
        document.addEventListener('click', clickOutsideHandler, true);
    }, 100);
}

export function cleanupClickOutside() {
    if (clickOutsideHandler) {
        document.removeEventListener('click', clickOutsideHandler, true);
        clickOutsideHandler = null;
    }
    themeSelectorElement = null;
}

export function calculateMenuPosition(buttonElement) {
    if (!buttonElement) return "left: 16px; top: 60px;";

    const rect = buttonElement.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const padding = 16;
    const isMobile = viewportWidth <= 768;

    // Calculate actual menu width (280px or viewport width - 32px, whichever is smaller)
    const menuWidth = Math.min(280, viewportWidth - (padding * 2));

    if (isMobile) {
        // On mobile, use fixed positioning with absolute coordinates
        let top = rect.bottom + 8;

        // Start by aligning with the button's left edge
        let left = rect.left;

        // If menu would overflow right edge of viewport, shift it left
        const menuRightEdge = left + menuWidth;
        if (menuRightEdge > viewportWidth - padding) {
            left = viewportWidth - menuWidth - padding;
        }

        // Ensure we don't go past the left edge
        if (left < padding) {
            left = padding;
        }

        // Keep menu within viewport vertically  
        const menuHeight = 400;
        if (top + menuHeight > viewportHeight - padding) {
            top = rect.top - menuHeight - 8;
            if (top < padding) {
                top = padding;
            }
        }

        return `left: ${left}px; top: ${top}px; width: ${menuWidth}px;`;
    } else {
        // On desktop, use relative positioning
        const spaceOnRight = viewportWidth - rect.right;

        if (spaceOnRight >= 280) {
            return "left: 0; top: 50px;";
        } else {
            return "right: 0; top: 50px;";
        }
    }
}
