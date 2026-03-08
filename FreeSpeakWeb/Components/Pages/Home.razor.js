// Store the cleanup function
let scrollCleanup = null;

// Scroll event handler for lazy loading
export function initializeInfiniteScroll(element, dotNetHelper) {
    // Element is no longer needed since we're using window scroll
    console.log('Initializing infinite scroll on window');

    let isScrolling = false;

    const handleScroll = () => {
        if (isScrolling) return;

        // Use window scroll position instead of element scroll
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const scrollHeight = document.documentElement.scrollHeight;
        const clientHeight = window.innerHeight;

        // Calculate if we're near the bottom (within 200px)
        const scrollThreshold = 200;
        const isNearBottom = scrollTop + clientHeight >= scrollHeight - scrollThreshold;

        if (isNearBottom) {
            console.log('Near bottom, loading more posts...');
            isScrolling = true;
            dotNetHelper.invokeMethodAsync('LoadMorePostsAsync')
                .then(() => {
                    console.log('Posts loaded successfully');
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

    // Clean up previous listener if it exists
    if (scrollCleanup) {
        scrollCleanup();
    }

    // Listen to window scroll instead of element scroll
    window.addEventListener('scroll', handleScroll);
    console.log('Scroll listener attached to window');

    // Store cleanup function
    scrollCleanup = () => {
        console.log('Cleaning up scroll listener');
        window.removeEventListener('scroll', handleScroll);
        scrollCleanup = null;
    };
}

export function disposeInfiniteScroll() {
    if (scrollCleanup) {
        scrollCleanup();
    }
}

export function scrollToTop(element) {
    // Scroll the window to top instead of the element
    window.scrollTo({
        top: 0,
        behavior: 'smooth'
    });
}
