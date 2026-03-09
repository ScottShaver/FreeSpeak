export function initializeAutoResize(textarea) {
    if (!textarea) return;

    function resize() {
        textarea.style.height = 'auto';
        const newHeight = Math.min(textarea.scrollHeight, 200);
        textarea.style.height = newHeight + 'px';
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

export function resizeTextarea(textarea) {
    if (!textarea) return;

    textarea.style.height = 'auto';
    const fullHeight = textarea.scrollHeight;
    const newHeight = Math.min(fullHeight, 200);
    textarea.style.height = newHeight + 'px';
    textarea.style.overflowY = fullHeight > 200 ? 'auto' : 'hidden';
}

export function calculateEmojiPickerPosition(buttonElement) {
    if (!buttonElement) return "left: 0; bottom: 40px;";

    const rect = buttonElement.getBoundingClientRect();
    const pickerWidth = 320;
    const pickerHeight = 300; // Approximate height

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
