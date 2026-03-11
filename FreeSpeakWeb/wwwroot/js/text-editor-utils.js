/**
 * Shared text editor utilities for textarea manipulation
 * Used across PostCreator, PostEditModal, and MultiLineCommentEditor components
 */

/**
 * Insert text at the current cursor position in a textarea
 * @param {HTMLTextAreaElement} textarea - The textarea element
 * @param {string} text - The text to insert
 * @returns {string} The new value of the textarea
 */
export function insertTextAtCursor(textarea, text) {
    if (!textarea) {
        console.error('Textarea element not found');
        return;
    }

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const currentValue = textarea.value;
    
    // Insert text at cursor position
    const newValue = currentValue.substring(0, start) + text + currentValue.substring(end);
    textarea.value = newValue;
    
    // Set cursor position after inserted text
    const newCursorPos = start + text.length;
    textarea.setSelectionRange(newCursorPos, newCursorPos);
    
    // Focus the textarea
    textarea.focus();
    
    // Trigger input event so Blazor binding updates
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    
    return newValue;
}

/**
 * Resize textarea to fit content with a maximum height
 * @param {HTMLTextAreaElement} textarea - The textarea element to resize
 * @param {number} maxHeight - Maximum height in pixels (default: 200)
 */
export function resizeTextarea(textarea, maxHeight = 200) {
    if (!textarea) return;

    // Reset height to auto to get the correct scrollHeight
    textarea.style.height = 'auto';

    // Get the full content height
    const fullHeight = textarea.scrollHeight;

    // Set the height to match the content, capped at maxHeight
    const newHeight = Math.min(fullHeight, maxHeight);
    textarea.style.height = newHeight + 'px';

    // Only show scrollbar when content exceeds max-height
    textarea.style.overflowY = fullHeight > maxHeight ? 'auto' : 'hidden';
}

/**
 * Reset textarea to its default single-line height
 * @param {HTMLTextAreaElement} textarea - The textarea element to reset
 */
export function resetTextarea(textarea) {
    if (!textarea) return;

    // Reset to single-line height
    textarea.style.height = 'auto';
    // Force a minimum height that corresponds to a single line
    textarea.style.height = '';
    textarea.style.overflowY = 'hidden';
}

/**
 * Replace textarea text while preserving cursor position
 * Adjusts cursor position based on text length difference
 * @param {HTMLTextAreaElement} textarea - The textarea element
 * @param {string} newText - The new text to set
 * @returns {string} The new text value
 */
export function replaceTextPreserveCursor(textarea, newText) {
    if (!textarea) return newText;

    // Save cursor position
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const oldText = textarea.value;

    // Update the value
    textarea.value = newText;

    // Calculate new cursor position
    // If text was replaced (emoji conversion), try to maintain relative position
    const lengthDiff = newText.length - oldText.length;
    let newCursorPos = start + lengthDiff;

    // Make sure cursor position is valid
    newCursorPos = Math.max(0, Math.min(newCursorPos, newText.length));

    // Restore cursor position
    textarea.setSelectionRange(newCursorPos, newCursorPos);

    // Trigger input event so Blazor binding updates
    textarea.dispatchEvent(new Event('input', { bubbles: true }));

    return newText;
}

/**
 * Initialize auto-resize behavior on a textarea
 * @param {HTMLTextAreaElement} textarea - The textarea element
 * @param {number} maxHeight - Maximum height in pixels (default: 200)
 * @returns {Object} Object with dispose method to cleanup event listeners
 */
export function initializeAutoResize(textarea, maxHeight = 200) {
    if (!textarea) return { dispose: () => {} };

    function resize() {
        textarea.style.height = 'auto';
        const newHeight = Math.min(textarea.scrollHeight, maxHeight);
        textarea.style.height = newHeight + 'px';
        textarea.style.overflowY = textarea.scrollHeight > maxHeight ? 'auto' : 'hidden';
    }

    // Initial resize
    resize();

    // Listen for input events
    textarea.addEventListener('input', resize);

    // Listen for keydown to catch Enter before text is added
    textarea.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') {
            // Use setTimeout to let the newline be added first
            setTimeout(resize, 0);
        }
    });

    return {
        dispose: function() {
            textarea.removeEventListener('input', resize);
        }
    };
}

/**
 * Blur (unfocus) the textarea
 * @param {HTMLTextAreaElement} textarea - The textarea element to blur
 */
export function blurTextarea(textarea) {
    if (textarea) {
        textarea.blur();
    }
}
