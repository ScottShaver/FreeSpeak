/**
 * Shared emoji picker utilities
 * Used to calculate and position emoji picker popups relative to trigger buttons
 */

/**
 * Calculate the position for an emoji picker popup relative to a button
 * Ensures the picker stays within viewport bounds
 * @param {HTMLElement} buttonElement - The button element that triggers the picker
 * @param {number} pickerWidth - Width of the emoji picker (default: 320)
 * @param {number} pickerHeight - Height of the emoji picker (default: 400)
 * @returns {string} CSS position style string (e.g., "left: 100px; top: 200px;")
 */
export function calculateEmojiPickerPosition(buttonElement, pickerWidth = 320, pickerHeight = 400) {
    if (!buttonElement) {
        return "left: 0; bottom: 40px;";
    }

    const rect = buttonElement.getBoundingClientRect();

    // Calculate if there's space above or below
    const spaceAbove = rect.top;
    const spaceBelow = window.innerHeight - rect.bottom;

    // Position above the button if there's not enough space below
    let top, left;

    if (spaceBelow < pickerHeight && spaceAbove > pickerHeight) {
        // Position above
        top = rect.top - pickerHeight - 8;
    } else {
        // Position below
        top = rect.bottom + 8;
    }

    // Position horizontally (align with button, but keep on screen)
    left = rect.left;

    // Make sure it doesn't go off the right edge
    if (left + pickerWidth > window.innerWidth) {
        left = window.innerWidth - pickerWidth - 16;
    }

    // Make sure it doesn't go off the left edge
    if (left < 16) {
        left = 16;
    }

    return `left: ${left}px; top: ${top}px;`;
}
