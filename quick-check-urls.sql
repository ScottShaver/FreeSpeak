-- DIAGNOSTIC: Check actual URLs in database right now
-- Copy results and paste them here

SELECT 'PROFILE PICTURES:' as check_type;
SELECT "UserName", "ProfilePictureUrl" 
FROM "AspNetUsers" 
WHERE "ProfilePictureUrl" IS NOT NULL
ORDER BY "UserName"
LIMIT 5;

SELECT 'POST IMAGES:' as check_type;
SELECT "Id", "ImageUrl"
FROM "PostImages"
ORDER BY "Id" DESC
LIMIT 5;
