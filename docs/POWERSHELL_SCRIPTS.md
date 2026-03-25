# PowerShell Scripts Guide

This guide documents the PowerShell scripts available in the FreeSpeak project for development, maintenance, and validation tasks.

---

## ValidateTranslations.ps1

### Overview

The `ValidateTranslations.ps1` script is an automated tool for auditing translation completeness across all FreeSpeak internationalization resources. It provides comprehensive reporting on missing translations, file status, and coverage statistics.

### Quick Start

### Basic Usage
```powershell
# Run complete audit of all translation groups
.\ValidateTranslations.ps1 -Action audit

# Run with execution policy bypass (if needed)
PowerShell -ExecutionPolicy Bypass -File .\ValidateTranslations.ps1 -Action audit
```

### Sample Output
```
=== TRANSLATION AUDIT ===
[ManageAccount] - Base keys: 67
  COMPLETE ar.resx
  COMPLETE de.resx
  COMPLETE es.resx
  COMPLETE fr.resx
  COMPLETE it.resx
  COMPLETE ja.resx
  COMPLETE ko.resx
  COMPLETE nl.resx
  COMPLETE pl.resx
  COMPLETE pt.resx
  COMPLETE ru.resx
  COMPLETE zh.resx
  Status: 12/12 files, 0 missing translations

[SystemAdmin] - Base keys: 45
  FILE MISSING ar.resx
  FILE MISSING de.resx
  COMPLETE ja.resx
  COMPLETE ko.resx
  COMPLETE zh.resx
  Status: 3/12 files, 405 missing translations

TOTAL MISSING: 405 translations
```

## Script Parameters

### Available Actions
- **`audit`** - Default action. Performs complete translation audit
- Additional actions can be added for future functionality

### Parameters
```powershell
param(
    [string]$Action = "audit",    # Action to perform
    [string]$Language = ""        # Future: Target specific language
)
```

## Resource Groups Monitored

The script automatically monitors these resource groups:

| Group | Path | Keys | Purpose |
|-------|------|------|---------|
| **ManageAccount** | `FreeSpeakWeb\Resources\Account\Pages\Manage\ManageAccount.resx` | 67 | Account management features |
| **Groups** | `FreeSpeakWeb\Resources\Pages\Groups.resx` | 32 | Group browsing and management |
| **GroupView** | `FreeSpeakWeb\Resources\Pages\GroupView.resx` | 13 | Individual group pages |
| **SystemAdmin** | `FreeSpeakWeb\Resources\Pages\SystemAdmin.resx` | 45 | Administrative interface |

### Supported Languages (12 Total)
`ar`, `de`, `es`, `fr`, `it`, `ja`, `ko`, `nl`, `pl`, `pt`, `ru`, `zh`

## Understanding the Output

### Status Indicators
- **✅ COMPLETE** - All keys present in language file
- **❌ MISSING** - Keys missing from language file (shows count)
- **⚠️ FILE MISSING** - Language file doesn't exist (all keys missing)

### Summary Statistics
- **Base keys** - Number of translatable strings in English base file
- **Status** - Files existing vs. total files, total missing translations
- **TOTAL MISSING** - Overall missing translations across all groups

## Maintenance Procedures

### Daily Development
Run before committing changes:
```powershell
# Quick check for new missing translations
.\ValidateTranslations.ps1 -Action audit | Select-String "MISSING"

# Full audit with detailed output
.\ValidateTranslations.ps1 -Action audit > translation-status.txt
```

### Weekly Team Checks
```powershell
# Generate status report for team review
.\ValidateTranslations.ps1 -Action audit | Tee-Object -FilePath "weekly-translation-report.txt"
```

### Release Preparation
```powershell
# Complete audit before release
.\ValidateTranslations.ps1 -Action audit
# Ensure "TOTAL MISSING: 0 translations" for user-facing features
```

## Extending the Script

### Adding New Resource Groups
Edit the `$ResourceGroups` hashtable in the script:

```powershell
$ResourceGroups = @{
    # ... existing groups ...
    "YourNewFeature" = @{
        BasePath = "FreeSpeakWeb\Resources\Pages\YourFeature.resx"
        Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
    }
}
```

### Future Enhancement Ideas
1. **CI/CD Integration** - Return exit codes for build pipeline integration
2. **Detailed Reporting** - JSON output for integration with other tools
3. **Translation Management** - Integration with professional translation services
4. **Quality Checks** - Validate translation quality and consistency

## Integration with Development Workflow

### Pre-commit Hook
Create `.git/hooks/pre-commit`:
```bash
#!/bin/sh
# Run translation validation before commit
powershell -ExecutionPolicy Bypass -File ValidateTranslations.ps1 -Action audit

# Check if user-facing features are complete
if (powershell -Command "& {.\ValidateTranslations.ps1 -Action audit | Select-String 'ManageAccount.*0 missing|Groups.*0 missing|GroupView.*0 missing'}").Count -eq 3
then
    echo "✅ User-facing translations complete"
else
    echo "❌ User-facing translations incomplete - see audit results above"
    echo "ℹ️  SystemAdmin translations can be incomplete (admin-only feature)"
fi
```

### GitHub Actions Integration
Add to `.github/workflows/validate-translations.yml`:
```yaml
name: Validate Translations
on: [push, pull_request]

jobs:
  validate:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Translation Audit
        shell: pwsh
        run: |
          .\ValidateTranslations.ps1 -Action audit

      - name: Check User-Facing Features
        shell: pwsh
        run: |
          $output = .\ValidateTranslations.ps1 -Action audit
          $userFacing = $output | Select-String "ManageAccount.*0 missing|Groups.*0 missing|GroupView.*0 missing"
          if ($userFacing.Count -eq 3) {
            Write-Host "✅ All user-facing features have complete translations"
          } else {
            Write-Host "❌ User-facing features missing translations"
            $output
            exit 1
          }
```

## Performance Considerations

### Script Performance
- **Execution time**: ~2-5 seconds for full audit
- **Memory usage**: Minimal - processes files sequentially
- **File I/O**: Reads resource files using PowerShell's XML parsing

### Optimization Tips
- **Cache results** during active development sessions
- **Parallel processing** could be added for large-scale projects
- **Incremental checks** for only changed files in CI/CD

## Troubleshooting

### Common Issues

#### PowerShell Execution Policy
**Error**: `running scripts is disabled on this system`
**Solution**:
```powershell
# Temporary bypass for current session
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Or run with bypass parameter
PowerShell -ExecutionPolicy Bypass -File .\ValidateTranslations.ps1 -Action audit
```

#### XML Parsing Errors
**Error**: Script fails with XML parsing errors
**Solution**:
```powershell
# Check for malformed resource files
Get-ChildItem -Path "FreeSpeakWeb\Resources" -Filter "*.resx" -Recurse | ForEach-Object { 
    try { 
        [xml](Get-Content $_.FullName -Encoding UTF8) 
        Write-Host "✅ $($_.Name)" -ForegroundColor Green
    } 
    catch { 
        Write-Host "❌ $($_.Name): $($_.Exception.Message)" -ForegroundColor Red
    } 
}
```

#### File Path Issues
**Error**: `Cannot find path` errors
**Solution**:
- Ensure running from solution root directory
- Check that resource files exist at expected paths
- Verify file naming conventions match script expectations

### Debug Mode
Add verbose output to the script:
```powershell
# Add at beginning of script for debugging
$VerbosePreference = "Continue"
Write-Verbose "Starting translation audit..."
```

## Best Practices

### Regular Usage
1. **Run before committing** any localization changes
2. **Include in pull request** templates and review process
3. **Monitor trending** - track missing translation counts over time
4. **Automate reporting** for team visibility

### Team Workflows
1. **Shared responsibility** - not just one person maintains translations
2. **Documentation** - keep this guide updated as script evolves
3. **Training** - ensure all developers know how to use the script
4. **Review process** - include translation audit in code reviews

### Quality Assurance
1. **Validate after adding keys** to ensure all languages updated
2. **Check before releases** to maintain professional quality
3. **Test with sample languages** to verify script accuracy
4. **Backup resource files** before bulk operations

---

*For comprehensive internationalization information, see [INTERNATIONALIZATION.md](INTERNATIONALIZATION.md) and [TRANSLATION_MAINTENANCE.md](TRANSLATION_MAINTENANCE.md)*