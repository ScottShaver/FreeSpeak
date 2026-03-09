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
