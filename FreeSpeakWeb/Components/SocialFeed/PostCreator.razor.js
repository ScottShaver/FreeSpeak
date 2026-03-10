// JavaScript functions for PostCreator component

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

export function calculateEmojiPickerPosition(button) {
    if (!button) {
        return "left: 0; bottom: 40px;";
    }

    const rect = button.getBoundingClientRect();
    const pickerWidth = 320;
    const pickerHeight = 400;

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

export function resizeTextarea(textarea) {
    if (!textarea) return;

    // Reset height to auto to get the correct scrollHeight
    textarea.style.height = 'auto';

    // Set the height to match the content
    const newHeight = Math.max(textarea.scrollHeight, 40); // Minimum 40px height
    textarea.style.height = newHeight + 'px';
}

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
