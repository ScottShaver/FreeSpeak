// BROWSER CONSOLE TEST
// Run this in browser console (F12 → Console) to force a request and see logs

console.log("Testing image resize service...");

// Test 1: Profile picture
fetch('/api/secure-files/profile-picture/c2b0565c-fb2c-4252-94c3-4cdd3758ac29')
  .then(r => r.blob())
  .then(blob => {
    console.log("✅ Profile picture loaded:", blob.size, "bytes");
    // Now check Output window in VS for logs with emojis
    // AND run: .\check-thumbnail-cache.ps1
  });

// Test 2: Post image  
fetch('/api/secure-files/post-image/bd50d737-04e5-434b-ba97-f6b7d6915b12/961d090d-008e-4db3-b7a1-8fb98736a91c/961d090d-008e-4db3-b7a1-8fb98736a91c.jpg')
  .then(r => r.blob())
  .then(blob => {
    console.log("✅ Post image loaded:", blob.size, "bytes");
    // Check Output window for logs
    // AND run: .\check-thumbnail-cache.ps1
  });

console.log("Requests sent! Check Visual Studio Output window (Debug pane) for emoji logs 🌐🎨✅");
console.log("Then run in PowerShell: .\\check-thumbnail-cache.ps1");
