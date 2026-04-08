# Translation Validation and Completion Script
param(
    [string]$Action = "audit",
    [string]$Language = ""
)

$ResourceGroups = @{
    # Account Resources
    "Login" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Pages\Login.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "Register" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Pages\Register.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ChangePassword" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Pages\Manage\ChangePassword.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ManageAccount" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Pages\Manage\ManageAccount.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UserPreferencesComponent" = @{
        BasePath = "FreeSpeakWeb\Resources\Account\Shared\UserPreferencesComponent.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Layout Resources
    "MainLayout" = @{
        BasePath = "FreeSpeakWeb\Resources\Layout\MainLayout.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "NavMenu" = @{
        BasePath = "FreeSpeakWeb\Resources\Layout\NavMenu.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Page Resources
    "Error" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\Error.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FriendsList" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\FriendsList.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FriendDetails" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\FriendDetails.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "Groups" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\Groups.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupFiles" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\GroupFiles.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupFilesPage" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\GroupFilesPage.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupView" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\GroupView.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "Home" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\Home.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MyUploads" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\MyUploads.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "Notifications" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\Notifications.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "PublicHome" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\PublicHome.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "SystemAdmin" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\SystemAdmin.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "NotFound" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\NotFound.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "SingleGroupPost" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\SingleGroupPost.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "SinglePost" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\SinglePost.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "SystemModerator" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\SystemModerator.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Shared Component Resources
    "AlertContainer" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\AlertContainer.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ConfirmationDialog" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ConfirmationDialog.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FriendProfileHeader" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\FriendProfileHeader.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "EmojiPicker" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\EmojiPicker.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ImageGalleryModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ImageGalleryModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ImagePreviewGrid" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ImagePreviewGrid.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ImageUploadModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ImageUploadModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ImageViewerModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ImageViewerModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MenuContainer" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\MenuContainer.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "NotificationComponent" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\NotificationComponent.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ThemeSelector" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\ThemeSelector.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UploadProgressModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\UploadProgressModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UserAvatar" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\UserAvatar.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MoreFunctions" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\MoreFunctions.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "TimeFormatting" = @{
        BasePath = "FreeSpeakWeb\Resources\Shared\TimeFormatting.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "NotificationTemplates" = @{
        BasePath = "FreeSpeakWeb\Resources\Notifications\NotificationTemplates.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Admin Resources
    "UserCreationModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Admin\UserCreationModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "AuditLogSearchModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Admin\AuditLogSearchModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UserAuditLogModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Admin\UserAuditLogModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UserLockoutManagementModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Admin\UserLockoutManagementModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UserRoleManagementModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Admin\UserRoleManagementModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Social Feed Resources
    "FeedArticle" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\FeedArticle.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FeedArticleActions" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\FeedArticleActions.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FeedArticleCommentEditor" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\FeedArticleCommentEditor.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FeedArticleHeader" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\FeedArticleHeader.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "FeedArticleImages" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\FeedArticleImages.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "LikesModal" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\LikesModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MultiLineCommentDisplay" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\MultiLineCommentDisplay.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MultiLineCommentEditor" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\MultiLineCommentEditor.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ReactionPicker" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\ReactionPicker.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "PostCreator" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\PostCreator.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "PostDetailModal" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\PostDetailModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "PostEditModal" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\PostEditModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "UnifiedArticle" = @{
        BasePath = "FreeSpeakWeb\Resources\SocialFeed\UnifiedArticle.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Friends Resources
    "FriendListItem" = @{
        BasePath = "FreeSpeakWeb\Resources\Friends\FriendListItem.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ProfilePreviewPopup" = @{
        BasePath = "FreeSpeakWeb\Resources\Friends\ProfilePreviewPopup.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }

    # Groups Resources
    "BannedMembersTab" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\BannedMembersTab.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupSettingsModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\GroupSettingsModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupAdministrationModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\GroupAdministrationModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "GroupModerationModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\GroupModerationModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "MemberManagementTab" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\MemberManagementTab.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "RulesAcceptanceModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\RulesAcceptanceModal.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
    "ReportContentModal" = @{
        BasePath = "FreeSpeakWeb\Resources\Groups\ReportContentModal.resx"
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