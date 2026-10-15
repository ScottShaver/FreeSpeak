let escapeHandler = null;

/**
 * Starts listening for keyboard events while a popup menu is open.
 * Escape closes the menu and returns focus to the anchor; ArrowUp/ArrowDown/Home/End move focus between menu items.
 *
 * @param {object} dotNetRef - .NET reference exposing a CloseFromKeyboardAsync method
 * @param {string} anchorElementId - ID of the element the menu is anchored to (focus returns to its button on close)
 */
export function register(dotNetRef, anchorElementId) {
    unregister();

    escapeHandler = (event) => {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            const anchor = anchorElementId ? document.getElementById(anchorElementId) : null;
            const trigger = anchor ? anchor.querySelector('button') : null;
            dotNetRef.invokeMethodAsync('CloseFromKeyboardAsync').then(() => {
                if (trigger) {
                    trigger.focus();
                }
            });
            return;
        }

        const navKeys = ['ArrowDown', 'ArrowUp', 'Home', 'End'];
        if (!navKeys.includes(event.key)) {
            return;
        }

        const items = Array.from(document.querySelectorAll('.popup-menu .popup-menu-item'));
        if (items.length === 0) {
            return;
        }

        event.preventDefault();
        const currentIndex = items.indexOf(document.activeElement);
        let nextIndex;
        if (event.key === 'Home') {
            nextIndex = 0;
        } else if (event.key === 'End') {
            nextIndex = items.length - 1;
        } else if (event.key === 'ArrowDown') {
            nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % items.length;
        } else {
            nextIndex = currentIndex < 0 ? items.length - 1 : (currentIndex - 1 + items.length) % items.length;
        }
        items[nextIndex].focus();
    };

    document.addEventListener('keydown', escapeHandler, true);
}

/**
 * Stops listening for popup menu keyboard events.
 */
export function unregister() {
    if (escapeHandler) {
        document.removeEventListener('keydown', escapeHandler, true);
        escapeHandler = null;
    }
}
