# Thumbnail Cache Verification Guide

## Quick Check Commands

Run these PowerShell commands to verify caching:

```powershell
# 1. Check if cache directory exists
Test-Path "FreeSpeakWeb\AppData\cache\resized-images"

# 2. Count cached thumbnails
(Get-ChildItem "FreeSpeakWeb\AppData\cache\resized-images" -File).Count

# 3. List all cached thumbnails with sizes
Get-ChildItem "FreeSpeakWeb\AppData\cache\resized-images" -File | 
    Select-Object Name, @{Name="Size(KB)";Expression={[math]::Round($_.Length/1KB,2)}}, LastWriteTime | 
    Format-Table -AutoSize

# 4. Watch for new cache files being created (run while browsing)
Get-ChildItem "FreeSpeakWeb\AppData\cache\resized-images" -File | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 5
```

## Expected Behavior

### First Request (Generates Thumbnail)
- **Time:** 50-150ms
- **Action:** Loads original image, resizes, saves to cache
- **Result:** Creates `{imageId}_thumbnail.jpg` in cache folder

### Second Request (From Cache)
- **Time:** 5-15ms  
- **Action:** Serves cached thumbnail directly
- **Result:** No new file created, faster response

## File Naming Convention

Cache files are named:
```
{imageId}_thumbnail.jpg  → 150px thumbnail
{imageId}_medium.jpg     → 400px medium size
```

Full size requests don't create cache files (serve original).

## Troubleshooting

### If no thumbnails are being cached:

1. **Check app is running**
   - Press F5 to start debugging
   - Verify app is on https://localhost:7025

2. **Verify images are being requested**
   - Open browser DevTools → Network tab
   - Navigate to Home or Profile page
   - Look for `/api/secure-files/profile-picture/...` requests
   - Status should be 200 OK

3. **Check logs for resize operations**
   - Look in Output window for:
   ```
   [INF] Created and cached resized image: Thumbnail at AppData/cache/resized-images/...
   ```

4. **Test manually in browser console**
   - Navigate to http://localhost:7025/test-thumbnail-cache.js
   - OR paste the test script from test-thumbnail-cache.js
   - Run it in console
   - Check cache folder immediately after

### Common Issues

**Issue 1: "No cached files"**
- ✅ **Solution:** Images only cached when requested. Browse to a page with images.

**Issue 2: "Cache folder empty after browsing"**
- ❌ **Problem:** App might not be using the new code
- ✅ **Solution:** Stop debugging (Shift+F5), rebuild (Ctrl+Shift+B), start again (F5)

**Issue 3: "Images not loading"**
- ❌ **Problem:** Database URLs might still have old format
- ✅ **Solution:** Run DataMigrationService (should happen automatically on startup)

## Manual Test Steps

1. **Stop your app** (Shift + F5)
2. **Rebuild** (Ctrl + Shift + B)
3. **Clear existing cache** (optional):
   ```powershell
   Remove-Item "FreeSpeakWeb\AppData\cache\resized-images\*" -Force
   ```
4. **Start app** (F5)
5. **Navigate to Home** (https://localhost:7025)
6. **Immediately check cache** after page loads:
   ```powershell
   Get-ChildItem "FreeSpeakWeb\AppData\cache\resized-images"
   ```

## Expected Results

After browsing the Home page with 10 posts:
- ✅ Cache folder should contain ~10-30 `.jpg` files
- ✅ Each file should be 5-25KB (thumbnails)
- ✅ Names like: `c2b0565c-fb2c-4252-94c3-4cdd3758ac29_thumbnail.jpg`

## Performance Metrics

**Before caching (first load):**
- Request time: ~100ms per image
- Total for 20 images: ~2000ms
- Bandwidth: ~40MB (full-size images)

**After caching (subsequent loads):**
- Request time: ~10ms per image
- Total for 20 images: ~200ms
- Bandwidth: ~300KB (thumbnails)

**Improvement:** 10x faster load, 99% less bandwidth!
