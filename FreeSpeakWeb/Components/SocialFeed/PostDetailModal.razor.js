let scrollHandler = null;

export function initializeCommentScroll(contentElement, dotNetHelper) {
    if (!contentElement) return;

    scrollHandler = async () => {
        const scrollTop = contentElement.scrollTop;
        const scrollHeight = contentElement.scrollHeight;
        const clientHeight = contentElement.clientHeight;

        // Check if user scrolled near bottom (within 100px)
        if (scrollTop + clientHeight >= scrollHeight - 100) {
            await dotNetHelper.invokeMethodAsync('LoadMoreCommentsAsync');
        }
    };

    contentElement.addEventListener('scroll', scrollHandler);
}

export function cleanupCommentScroll() {
    if (scrollHandler) {
        scrollHandler = null;
    }
}
