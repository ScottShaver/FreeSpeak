// Store the cleanup function
let scrollCleanup = null;

// Scroll event handler for lazy loading
export function initializeInfiniteScroll(element, dotNetHelper) {
    let isScrolling = false;

    const handleScroll = () => {
        if (isScrolling) return;

        // Use window scroll position
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const scrollHeight = document.documentElement.scrollHeight;
        const clientHeight = window.innerHeight;

        // Calculate if we're near the bottom (within 200px)
        const scrollThreshold = 200;
        const isNearBottom = scrollTop + clientHeight >= scrollHeight - scrollThreshold;

        if (isNearBottom) {
            isScrolling = true;

            // Determine which tab is active
            const groupFeedTab = document.getElementById('group-feed-content');
            const pinnedGroupTab = document.getElementById('pinned-group-content');

            let loadMethod = 'LoadMoreGroupPostsAsync'; // Default

            if (pinnedGroupTab && pinnedGroupTab.classList.contains('active')) {
                loadMethod = 'LoadMorePinnedGroupPostsAsync';
            }

            dotNetHelper.invokeMethodAsync(loadMethod)
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

    // Add the scroll event listener
    window.addEventListener('scroll', handleScroll);

    // Store the cleanup function
    scrollCleanup = () => {
        window.removeEventListener('scroll', handleScroll);
    };

    console.log('Groups infinite scroll initialized');
}

// Cleanup function
export function cleanupInfiniteScroll() {
    if (scrollCleanup) {
        scrollCleanup();
        scrollCleanup = null;
    }
    console.log('Groups infinite scroll cleaned up');
}
