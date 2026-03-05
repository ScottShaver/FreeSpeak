export function shouldAutoCloseNav() {
    // Check if it's a mobile device
    const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

    // Check if window is narrow (Bootstrap's lg breakpoint is 992px)
    const isNarrowWindow = window.innerWidth < 992;

    // Auto-close on mobile OR when window is narrow on desktop
    return isMobile || isNarrowWindow;
}

export function isWideScreen() {
    // Return true if we should show the menu by default (desktop/wide screen)
    return window.innerWidth >= 992;
}

export function setupResizeHandler(dotnetHelper) {
    let wasNarrow = window.innerWidth < 992;

    const handleResize = () => {
        const isNarrow = window.innerWidth < 992;

        // Only trigger if the state changed
        if (wasNarrow !== isNarrow) {
            if (isNarrow) {
                // Window became narrow - collapse the menu
                dotnetHelper.invokeMethodAsync('OnWindowResizedToNarrow');
            } else {
                // Window became wide - expand the menu
                dotnetHelper.invokeMethodAsync('OnWindowResizedToWide');
            }
            wasNarrow = isNarrow;
        }
    };

    window.addEventListener('resize', handleResize);

    // Return a cleanup function
    return {
        dispose: () => {
            window.removeEventListener('resize', handleResize);
        }
    };
}
