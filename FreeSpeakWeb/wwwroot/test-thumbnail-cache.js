// Test script to verify ImageResizingService thumbnail caching
// Run this in the browser console while logged in to test

async function testThumbnailGeneration() {
    console.log("Testing thumbnail generation...");
    
    // Get a profile picture (replace with actual userId)
    const testUserId = "c2b0565c-fb2c-4252-94c3-4cdd3758ac29";
    
    console.time("First request (generates thumbnail)");
    const response1 = await fetch(`/api/secure-files/profile-picture/${testUserId}`);
    console.timeEnd("First request (generates thumbnail)");
    console.log("Response 1:", response1.status, response1.headers.get('content-length'));
    
    // Second request should be from cache
    console.time("Second request (from cache)");
    const response2 = await fetch(`/api/secure-files/profile-picture/${testUserId}`);
    console.timeEnd("Second request (from cache)");
    console.log("Response 2:", response2.status, response2.headers.get('content-length'));
    
    // Test with size parameter
    console.time("Medium size request");
    const response3 = await fetch(`/api/secure-files/profile-picture/${testUserId}?size=medium`);
    console.timeEnd("Medium size request");
    console.log("Response 3 (medium):", response3.status, response3.headers.get('content-length'));
    
    console.log("✅ Test complete! Check AppData/cache/resized-images/ for cached thumbnails");
}

// Run the test
testThumbnailGeneration();
