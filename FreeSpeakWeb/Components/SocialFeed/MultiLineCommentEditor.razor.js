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
