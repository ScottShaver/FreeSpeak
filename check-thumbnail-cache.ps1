# Quick Test - Run this after restarting your app and viewing the home page

Write-Host "Checking for cached thumbnails..." -ForegroundColor Cyan
Write-Host ""

$cachePath = "FreeSpeakWeb\AppData\cache\resized-images"

if (Test-Path $cachePath) {
    $files = Get-ChildItem $cachePath -File
    
    if ($files.Count -eq 0) {
        Write-Host "❌ Cache folder exists but is EMPTY" -ForegroundColor Yellow
        Write-Host "   This means either:" -ForegroundColor Yellow
        Write-Host "   1. App hasn't been restarted with new code" -ForegroundColor Yellow
        Write-Host "   2. No pages with images have been viewed yet" -ForegroundColor Yellow
        Write-Host "   3. Images are being served from wwwroot (old code)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "✅ Solution: Stop app (Shift+F5), Rebuild (Ctrl+Shift+B), Start (F5), then browse to Home page" -ForegroundColor Green
    } else {
        Write-Host "✅ SUCCESS! Found $($files.Count) cached thumbnails!" -ForegroundColor Green
        Write-Host ""
        $files | ForEach-Object {
            $sizeKB = [math]::Round($_.Length / 1KB, 2)
            Write-Host "  📷 $($_.Name) - ${sizeKB}KB - Created: $($_.LastWriteTime.ToString('HH:mm:ss'))" -ForegroundColor White
        }
        Write-Host ""
        $totalKB = [math]::Round(($files | Measure-Object -Property Length -Sum).Sum / 1KB, 2)
        Write-Host "  Total cache size: ${totalKB}KB" -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ Cache directory doesn't exist yet" -ForegroundColor Red
    Write-Host "   Creating it now..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
    Write-Host "✅ Created: $cachePath" -ForegroundColor Green
    Write-Host "   Now restart your app and browse to a page with images" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "To manually test, run in browser console:" -ForegroundColor Cyan
Write-Host "  fetch('/api/secure-files/profile-picture/c2b0565c-fb2c-4252-94c3-4cdd3758ac29')" -ForegroundColor White
