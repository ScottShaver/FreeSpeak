# .gitignore Update - Security Migration

## Changes Made

Updated `.gitignore` to reflect the new secure file storage locations after moving user uploads outside of `wwwroot`.

### Old Location (Insecure - Public Access)
```
wwwroot/images/profiles/        → Profile pictures
wwwroot/uploads/posts/          → Post images and videos
```

### New Location (Secure - API Access Only)
```
AppData/images/profiles/        → Profile pictures
AppData/uploads/posts/          → Post images and videos
```

## What's Ignored

The following patterns are now in `.gitignore`:

```gitignore
# User-uploaded files (secure location)
/FreeSpeakWeb/AppData/images/profiles/**/*
/FreeSpeakWeb/AppData/uploads/posts/**/*

# Keep directory structure
!/FreeSpeakWeb/AppData/images/profiles/.gitkeep
!/FreeSpeakWeb/AppData/uploads/posts/**/.gitkeep
```

## Directory Structure Preserved

`.gitkeep` files have been added to maintain the directory structure in Git:
- ✅ `FreeSpeakWeb/AppData/images/profiles/.gitkeep`
- ✅ `FreeSpeakWeb/AppData/uploads/posts/.gitkeep`

## Old Entries

The old `wwwroot` entries have been commented out in `.gitignore` for reference:
```gitignore
# OLD LOCATION (deprecated - kept for reference, remove after migration)
# /FreeSpeakWeb/wwwroot/images/profiles
# /FreeSpeakWeb/wwwroot/uploads/posts/**/*
```

## Why This Matters

**Security:** Files in `AppData` are:
- ✅ Outside the public `wwwroot` folder
- ✅ Not directly accessible via URLs
- ✅ Served only through authenticated API endpoints
- ✅ Subject to permission checks (Public/FriendsOnly/MeOnly)

**Git Best Practice:**
- User-generated content should never be in version control
- Each developer/environment will have different test uploads
- Production files are different from development files
- Keeps repository size manageable

## Verification

After these changes:
1. ✅ New uploads go to `AppData/` (secure)
2. ✅ `AppData/` content is not tracked by Git
3. ✅ Directory structure is preserved via `.gitkeep`
4. ✅ Old `wwwroot/uploads/` entries remain ignored (if files still exist)

## Next Steps

### Optional Cleanup
Once you've verified the migration is complete, you can:

1. **Remove old wwwroot uploads:**
   ```powershell
   Remove-Item -Recurse -Force FreeSpeakWeb\wwwroot\uploads\posts
   Remove-Item -Recurse -Force FreeSpeakWeb\wwwroot\images\profiles
   ```

2. **Remove old gitignore comments:**
   Delete the commented-out lines for wwwroot in `.gitignore`

3. **Commit the changes:**
   ```bash
   git add .gitignore
   git add FreeSpeakWeb/AppData/images/profiles/.gitkeep
   git add FreeSpeakWeb/AppData/uploads/posts/.gitkeep
   git commit -m "Security: Move uploads to AppData, update .gitignore"
   ```

## Testing

To verify `.gitignore` is working:
1. Upload a test image through the app
2. Check that it appears in `AppData/`
3. Run `git status` - the uploaded file should NOT appear
4. Only `.gitkeep` files should be tracked

---

**Part of Security Implementation** - See `SECURITY_IMPLEMENTATION_SUMMARY.md` for full details.
