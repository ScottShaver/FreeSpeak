// Store the cleanup function
let scrollCleanup = null;
let currentTabType = null;

// Scroll event handler for lazy loading
export function initializeInfiniteScroll(element, dotNetHelper, tabType) {
    currentTabType = tabType;

    let isScrolling = false;

    const handleScroll = () => {
        if (isScrolling) return;

        // Use window scroll position
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const scrollHeight = document.documentElement.scrollHeight;
        const clientHeight = window.innerHeight;

        // Calculate if we're near the bottom (within 300px)
        const scrollThreshold = 300;
        const isNearBottom = scrollTop + clientHeight >= scrollHeight - scrollThreshold;

        if (isNearBottom) {
            isScrolling = true;
            
            const methodName = tabType === 'pictures' ? 'LoadMorePicturesAsync' : 'LoadMoreVideosAsync';
            
            dotNetHelper.invokeMethodAsync(methodName)
                .then(() => {
                    // Reset the flag after a short delay to prevent rapid firing
                    setTimeout(() => {
                        isScrolling = false;
                    }, 1000);
                })
                .catch(err => {
                    console.error(`Error loading more ${tabType}:`, err);
                    isScrolling = false;
                });
        }
    };

    // Clean up previous listener if it exists
    if (scrollCleanup) {
        scrollCleanup();
    }

    // Listen to window scroll
    window.addEventListener('scroll', handleScroll);

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
