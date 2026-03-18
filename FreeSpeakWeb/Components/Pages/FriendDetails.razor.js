// Store the cleanup function
let scrollCleanup = null;

// Scroll event handler for lazy loading
export function initializeInfiniteScroll(element, dotNetHelper) {
    // Clean up any existing listener first
    if (scrollCleanup) {
        scrollCleanup();
    }

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
            isScrolling = true;
            try {
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
            } catch (err) {
                console.error('Error invoking LoadMorePostsAsync:', err);
                isScrolling = false;
            }
        }
    };

    // Listen to window scroll with passive option for better performance
    window.addEventListener('scroll', handleScroll, { passive: true });

    // Store cleanup function
    scrollCleanup = () => {
        window.removeEventListener('scroll', handleScroll);
        scrollCleanup = null;
    };
}

export function disposeInfiniteScroll() {
    if (scrollCleanup) {
        scrollCleanup();
    }
}

export function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
}