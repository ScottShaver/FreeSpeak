export function initializeContentMeasurement(contentElement, dotNetHelper) {
    if (!contentElement) return;

    // Calculate line height
    const computedStyle = window.getComputedStyle(contentElement);
    const lineHeight = parseFloat(computedStyle.lineHeight);
    const maxLines = 8;
    const maxHeight = lineHeight * maxLines;

    // Check if content exceeds max lines
    const actualHeight = contentElement.scrollHeight;
    const isOverflowing = actualHeight > maxHeight;

    dotNetHelper.invokeMethodAsync('SetContentOverflow', isOverflowing);
}

export function cleanupContentMeasurement() {
    // Cleanup if needed in the future
}
