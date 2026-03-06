// Scroll event handler for lazy loading
export function initializeInfiniteScroll(element, dotNetHelper) {
    if (!element) {
        console.error('Feed element not found');
        return;
    }

    let isScrolling = false;

    const handleScroll = () => {
        if (isScrolling) return;

        const scrollTop = element.scrollTop;
        const scrollHeight = element.scrollHeight;
        const clientHeight = element.clientHeight;

        // Calculate if we're near the bottom (within 200px)
        const scrollThreshold = 200;
        const isNearBottom = scrollTop + clientHeight >= scrollHeight - scrollThreshold;

        if (isNearBottom) {
            isScrolling = true;
            dotNetHelper.invokeMethodAsync('LoadMorePostsAsync')
                .then(() => {
                    // Reset the flag after a short delay to prevent rapid firing
                    setTimeout(() => {
                        isScrolling = false;
                    }, 1000);
                })
                .catch(err => {
                    console.error('Error loading more posts:', err);
                    isScrolling = false;
                });
        }
    };

    element.addEventListener('scroll', handleScroll);

    // Return cleanup function
    return () => {
        element.removeEventListener('scroll', handleScroll);
    };
}

export function disposeInfiniteScroll(element) {
    // Cleanup is handled by the returned function from initializeInfiniteScroll
}

export function scrollToTop(element) {
    if (element) {
        element.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    }
}
