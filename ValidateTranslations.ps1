# Translation Validation and Completion Script
param(
    [string]$Action = "audit",
    [string]$Language = ""
)

$ResourceGroups = @{
    "ManageAccount" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Pages\Manage\ManageAccount.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "Groups" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\Groups.resx" 
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupView" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\GroupView.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "SystemAdmin" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\SystemAdmin.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
}

function Get-ResourceKeys {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        return @()
    }

    $keys = Select-String -Path $FilePath -Pattern '<data name="([^"]+)"' | ForEach-Object { 
        $_.Matches.Groups[1].Value 
    }
    return $keys
}

function Show-CompletionStatus {
    Write-Host "=== TRANSLATION AUDIT ===" -ForegroundColor Yellow

    $totalMissing = 0

    foreach ($groupName in $ResourceGroups.Keys) {
        $group = $ResourceGroups[$groupName]
        $baseFile = $group.BasePath

        if (-not (Test-Path $baseFile)) {
            Write-Host "[$groupName] - BASE FILE NOT FOUND" -ForegroundColor Red
            continue
        }

        $baseKeys = Get-ResourceKeys $baseFile
        Write-Host "[$groupName] - Base keys: $($baseKeys.Count)" -ForegroundColor Cyan

        $groupMissing = 0
        $existingFiles = 0

        foreach ($lang in $group.Languages) {
            $langFile = $baseFile -replace "\.resx$", ".$lang.resx"

            if (Test-Path $langFile) {
                $existingFiles++
                $langKeys = Get-ResourceKeys $langFile
                $missingKeys = $baseKeys | Where-Object { $_ -notin $langKeys }

                if ($missingKeys.Count -gt 0) {
                    Write-Host "  MISSING $lang.resx: $($missingKeys.Count) keys" -ForegroundColor Red
                    $groupMissing += $missingKeys.Count
                } else {
                    Write-Host "  COMPLETE $lang.resx" -ForegroundColor Green
                }
            } else {
                Write-Host "  FILE MISSING $lang.resx" -ForegroundColor Yellow
                $groupMissing += $baseKeys.Count
            }
        }

        Write-Host "  Status: $existingFiles/$($group.Languages.Count) files, $groupMissing missing translations" -ForegroundColor Gray
        $totalMissing += $groupMissing
    }

    Write-Host "TOTAL MISSING: $totalMissing translations" -ForegroundColor Yellow
}

switch ($Action.ToLower()) {
    "audit" { 
        Show-CompletionStatus 
    }
    default { 
        Write-Host "Usage: PowerShell -ExecutionPolicy Bypass -File ValidateTranslations.ps1 -Action audit"
    }
}