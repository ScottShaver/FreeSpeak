-- Migration script to update profile picture and post image URLs to use secure endpoints
-- Run this script if you have existing data that needs to be migrated

-- 1. Update profile picture URLs
UPDATE "AspNetUsers" 
SET "ProfilePictureUrl" = REPLACE("ProfilePictureUrl", '/api/profile-picture/', '/api/secure-files/profile-picture/')
WHERE "ProfilePictureUrl" LIKE '/api/profile-picture/%';

-- 2. Update post image URLs (if any exist with old format)
-- Note: New images created after this change will already use the correct format
UPDATE "PostImages"
SET "ImageUrl" = 
    '/api/secure-files/post-image/' || 
    SPLIT_PART("ImageUrl", '/', 4) || '/' ||  -- userId
    SPLIT_PART(SPLIT_PART("ImageUrl", '/', 6), '.', 1) || '/' ||  -- imageId (GUID from filename)
    SPLIT_PART("ImageUrl", '/', 6)  -- filename
WHERE "ImageUrl" LIKE '/uploads/posts/%/images/%';

-- Verify the changes
SELECT "Id", "UserName", "ProfilePictureUrl" 
FROM "AspNetUsers" 
WHERE "ProfilePictureUrl" IS NOT NULL;

SELECT "Id", "PostId", "ImageUrl" 
FROM "PostImages" 
WHERE "ImageUrl" IS NOT NULL;
