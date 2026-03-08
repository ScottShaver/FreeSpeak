-- Check current URLs in database
-- Run this in your PostgreSQL database

-- Check profile picture URLs
SELECT "Id", "UserName", "ProfilePictureUrl" 
FROM "AspNetUsers" 
WHERE "ProfilePictureUrl" IS NOT NULL
ORDER BY "UserName"
LIMIT 20;

-- Check post image URLs  
SELECT "Id", "PostId", "ImageUrl"
FROM "PostImages"
ORDER BY "Id" DESC
LIMIT 20;

-- Expected formats:
-- CORRECT (NEW):
--   /api/secure-files/profile-picture/{userId}
--   /api/secure-files/post-image/{userId}/{imageId}/{filename}
--
-- WRONG (OLD):
--   /api/profile-picture/{userId}
--   /uploads/posts/{userId}/images/{filename}
