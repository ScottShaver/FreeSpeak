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

    // Add class to modal container based on whether post has images
    applyNoImagesClass(contentElement);
}

export function applyNoImagesClass(contentElement) {
    if (!contentElement) return;

    const feedArticle = contentElement.querySelector('.feed-article');
    if (!feedArticle) return;

    const articleImages = feedArticle.querySelector('.article-images');
    const hasImages = articleImages && articleImages.children.length > 0;

    if (!hasImages) {
        feedArticle.classList.add('no-images');
    } else {
        feedArticle.classList.remove('no-images');
    }
}

export function cleanupCommentScroll() {
    if (scrollHandler) {
        scrollHandler = null;
    }
}

export function scrollToAndHighlightComment(commentId) {
    // Find the comment element by data attribute or ID
    // GroupPostArticle uses data-comment-id attribute for comments
    const commentElement = document.querySelector(`[data-comment-id="${commentId}"]`);

    if (commentElement) {
        // Add highlight class
        commentElement.classList.add('comment-highlight');

        // Scroll into view with smooth behavior
        commentElement.scrollIntoView({
            behavior: 'smooth',
            block: 'center',
            inline: 'nearest'
        });

        // Remove the highlight class after the animation completes (2 seconds)
        setTimeout(() => {
            commentElement.classList.remove('comment-highlight');
        }, 2000);
    } else {
        console.warn(`Comment with ID ${commentId} not found in DOM`);
    }
}
