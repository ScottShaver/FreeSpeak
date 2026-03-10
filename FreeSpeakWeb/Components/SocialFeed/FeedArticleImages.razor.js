export function scrollContainer(container, direction) {
    if (!container) return;
    
    const scrollAmount = container.offsetWidth * 0.8;
    const scrollValue = direction === 'left' ? -scrollAmount : scrollAmount;
    
    container.scrollBy({
        left: scrollValue,
        behavior: 'smooth'
    });
}
