// JavaScript for UnifiedArticle content measurement functionality

let resizeObserver = null;
let storedDotNetHelper = null;
let storedContentElement = null;

/**
 * Measures whether the content element is overflowing (truncated).
 * @param {HTMLElement} contentElement - The content element to measure.
 * @param {object} dotNetHelper - The .NET interop helper.
 */
function measureContent(contentElement, dotNetHelper) {
    if (!contentElement || !dotNetHelper) return;

    try {
        // Check if content is overflowing (scrollHeight > clientHeight)
        const isOverflowing = contentElement.scrollHeight > contentElement.clientHeight;
        dotNetHelper.invokeMethodAsync('SetContentOverflow', isOverflowing);
    } catch (error) {
        console.warn('Failed to measure content overflow:', error);
    }
}

/**
 * Initializes content measurement for detecting truncated posts.
 * @param {HTMLElement} contentElement - The content element to observe.
 * @param {object} dotNetHelper - The .NET interop helper.
 */
export function initializeContentMeasurement(contentElement, dotNetHelper) {
    if (!contentElement) return;

    storedContentElement = contentElement;
    storedDotNetHelper = dotNetHelper;

    try {
        // Initial measurement
        measureContent(contentElement, dotNetHelper);

        // Set up ResizeObserver to detect dynamic changes (e.g., window resize)
        if (typeof ResizeObserver !== 'undefined') {
            resizeObserver = new ResizeObserver(() => {
                measureContent(storedContentElement, storedDotNetHelper);
            });
            resizeObserver.observe(contentElement);
        }
    } catch (error) {
        console.warn('Failed to initialize content measurement:', error);
    }
}

/**
 * Cleans up content measurement resources.
 */
export function cleanup() {
    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }
    storedDotNetHelper = null;
    storedContentElement = null;
}

/**
 * Cleans up content measurement - alias for backward compatibility.
 */
export function cleanupContentMeasurement() {
    cleanup();
}