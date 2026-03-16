# Translation Maintenance Guide

This guide provides step-by-step instructions for maintaining and extending FreeSpeak's internationalization system.

## Quick Reference

### Validation Script
```powershell
# Check all translation status
.\ValidateTranslations.ps1 -Action audit

# Get detailed breakdown
PowerShell -ExecutionPolicy Bypass -File .\ValidateTranslations.ps1 -Action audit
```

### Common Tasks
- **Adding new translation key**: Add to base `.resx` → Update all language files → Validate → Test
- **Checking completeness**: Run validation script after any changes
- **Build verification**: `dotnet build` to ensure all translations compile

## Adding New Translation Keys

### Step 1: Add to Base English File
1. **Locate the appropriate resource file**:
   - Account features: `FreeSpeakWeb/Resources/Account/Pages/Manage/ManageAccount.resx`
   - Group features: `FreeSpeakWeb/Resources/Pages/Groups.resx`
   - Navigation: `FreeSpeakWeb/Resources/Layout/NavMenu.resx`
   - New features: Create new resource group

2. **Add the key to the base English file**:
   ```xml
   <data name="YourNewKey" xml:space="preserve">
     <value>Your English Text</value>
   </data>
   ```

3. **Use descriptive key names**:
   - ✅ Good: `DeleteAccountWarning`, `LoadingUserData`, `SaveSuccessMessage`
   - ❌ Bad: `Text1`, `Message`, `Button`

### Step 2: Update All Language Files
1. **Run validation to see missing keys**:
   ```powershell
   .\ValidateTranslations.ps1 -Action audit
   ```

2. **Add to each language file** (12 languages total):
   ```xml
   <!-- German example -->
   <data name="YourNewKey" xml:space="preserve">
     <value>Ihr deutscher Text</value>
   </data>
   ```

3. **Language file locations**:
   - `ManageAccount.ar.resx` (Arabic)
   - `ManageAccount.de.resx` (German)  
   - `ManageAccount.es.resx` (Spanish)
   - `ManageAccount.fr.resx` (French)
   - `ManageAccount.it.resx` (Italian)
   - `ManageAccount.ja.resx` (Japanese)
   - `ManageAccount.ko.resx` (Korean)
   - `ManageAccount.nl.resx` (Dutch)
   - `ManageAccount.pl.resx` (Polish)
   - `ManageAccount.pt.resx` (Portuguese)
   - `ManageAccount.ru.resx` (Russian)
   - `ManageAccount.zh.resx` (Chinese)

### Step 3: Use in Components
```razor
@using Microsoft.Extensions.Localization
@inject IStringLocalizer<FreeSpeakWeb.Resources.Pages.YourResourceGroup> Localizer

<h1>@Localizer["YourNewKey"]</h1>
```

### Step 4: Validate and Test
1. **Check completeness**:
   ```powershell
   .\ValidateTranslations.ps1 -Action audit
   ```

2. **Build project**:
   ```bash
   dotnet build
   ```

3. **Test in browser** with different languages

## Creating New Resource Groups

### When to Create New Groups
- **New feature areas** (e.g., messaging, marketplace)
- **Large components** with many strings (10+ keys)
- **Shared components** used across multiple pages

### Steps to Create New Group
1. **Create base English file**:
   ```
   FreeSpeakWeb/Resources/Pages/YourFeature.resx
   ```

2. **Add to validation script**:
   ```powershell
   # Edit ValidateTranslations.ps1
   $ResourceGroups = @{
       # ... existing groups ...
       "YourFeature" = @{
           BasePath = "FreeSpeakWeb\Resources\Pages\YourFeature.resx"
           Languages = @("ar", "de", "es", "fr", "it", "ja", "ko", "nl", "pl", "pt", "ru", "zh")
       }
   }
   ```

3. **Create all 12 language files** using the established pattern

4. **Update component injection**:
   ```razor
   @inject IStringLocalizer<FreeSpeakWeb.Resources.Pages.YourFeature> Localizer
   ```

## Translation Guidelines

### Key Naming Conventions
- **PascalCase**: `SaveChanges`, `UserProfile`
- **Descriptive**: Clearly indicate purpose and context
- **Consistent**: Use similar patterns for related features
- **Avoid abbreviations**: `DeleteAccount` not `DelAcc`

### Translation Quality
- **Context-aware**: Consider UI placement and space constraints
- **Culturally appropriate**: Adapt idioms and expressions
- **Consistent terminology**: Maintain same terms across features
- **Length consideration**: German text can be 30% longer than English

### Parameterized Messages
For dynamic content:
```xml
<data name="FoundResults" xml:space="preserve">
  <value>Found {0} result(s)</value>
</data>
```

Usage in code:
```csharp
string message = Localizer["FoundResults", resultCount];
```

## Maintenance Procedures

### Weekly Checks
1. **Run validation script** to ensure completeness
2. **Check for new untranslated keys** in pull requests
3. **Verify build status** after translation updates

### Release Preparation
1. **Full validation audit**:
   ```powershell
   .\ValidateTranslations.ps1 -Action audit > translation-status.txt
   ```

2. **Test with sample languages**:
   - English (base)
   - German (text expansion)
   - Arabic (RTL)
   - Japanese (character encoding)

3. **Performance check**: Ensure resource loading doesn't impact startup time

### Updating Existing Translations
1. **Identify the key** to update
2. **Update base English** first
3. **Update corresponding language files**
4. **Test in context** to ensure UI fits properly
5. **Document changes** if terminology shifts

## Troubleshooting

### Common Issues

#### Missing Translation (Shows Key Name)
**Symptoms**: `@Localizer["MyKey"]` displays "MyKey" instead of translated text
**Causes**:
- Key not present in language resource file
- Typo in key name
- Resource file compilation error

**Solutions**:
```powershell
# 1. Check if key exists in language file
Select-String -Path "FreeSpeakWeb\Resources\**\*.resx" -Pattern "MyKey"

# 2. Validate all files
.\ValidateTranslations.ps1 -Action audit

# 3. Check for XML syntax errors
Get-ChildItem -Path "FreeSpeakWeb\Resources" -Filter "*.resx" -Recurse | ForEach-Object { 
    try { [xml](Get-Content $_.FullName -Encoding UTF8) } 
    catch { Write-Host "XML Error in: $($_.Name)" -ForegroundColor Red } 
}
```

#### Build Errors in Resource Files
**Symptoms**: Compilation fails with resource-related errors
**Causes**:
- Malformed XML in `.resx` files
- Duplicate key names
- Invalid characters in keys or values

**Solutions**:
1. **Validate XML syntax** using the script above
2. **Check for duplicates**:
   ```powershell
   # Find duplicate keys in a file
   $content = Get-Content "path\to\file.resx"
   $keys = $content | Select-String 'name="([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }
   $keys | Group-Object | Where-Object Count -gt 1
   ```

#### Language Not Switching
**Symptoms**: Browser language change doesn't affect displayed language
**Causes**:
- Browser cache
- Culture detection not working
- Resource files not loaded

**Solutions**:
1. **Clear browser cache** completely
2. **Test with incognito/private window**
3. **Check browser Accept-Language header**
4. **Verify resource file compilation**

### Debug Commands

#### Check Resource Compilation
```bash
# Verify resources are built into assemblies
dotnet build --verbosity detailed | findstr "resx"
```

#### Test Language Detection
```csharp
// Add to a page for debugging
@inject IWebHostEnvironment Environment
@inject IOptions<RequestLocalizationOptions> LocalizationOptions

<p>Current Culture: @System.Globalization.CultureInfo.CurrentCulture</p>
<p>Current UI Culture: @System.Globalization.CultureInfo.CurrentUICulture</p>
<p>Supported Cultures: @string.Join(", ", LocalizationOptions.Value.SupportedCultures.Select(c => c.Name))</p>
```

## Performance Considerations

### Resource Loading
- **Lazy loading**: Resources loaded only when needed
- **Memory caching**: Loaded resources cached for application lifetime
- **Satellite assemblies**: Each language compiled to separate assembly

### Bundle Size Management
- **Base application**: ~50KB English resources
- **Per language**: ~45-55KB additional overhead
- **Total with all 12 languages**: ~650KB
- **Gzip compression**: Reduces size by ~60-70%

### Best Practices
- **Minimize resource keys**: Don't create keys for single-use strings
- **Group related strings**: Use consistent resource file organization
- **Avoid runtime string building**: Pre-translate complete messages when possible

## Contributing Guidelines

### For Developers
1. **Always add English key first** before creating language variants
2. **Use validation script** before committing changes
3. **Test with multiple languages** during development
4. **Update documentation** when adding new resource groups

### For Translators
1. **Maintain consistency** in terminology across features
2. **Consider UI constraints** when translating (button text length, etc.)
3. **Ask for context** if translation meaning is unclear
4. **Test translations in actual UI** when possible

### Code Review Checklist
- [ ] New keys added to base English file
- [ ] All 12 language files updated
- [ ] Validation script passes
- [ ] Build succeeds without errors
- [ ] UI tested with sample languages
- [ ] Documentation updated if new resource group

## Automation Opportunities

### Future Enhancements
1. **CI/CD integration**: Automatically run validation script in build pipeline
2. **Translation management platform**: Integration with professional translation services
3. **Missing key detection**: Automated detection of untranslated keys in PRs
4. **Quality assurance**: Automated testing of translation loading

### Suggested Tools
- **Azure Cognitive Services Translator**: For machine translation base
- **Phrase**: Professional translation management platform  
- **GitHub Actions**: Automated validation in CI/CD
- **PowerShell DSC**: Configuration management for translation workflows

---

*For detailed internationalization information, see [INTERNATIONALIZATION.md](INTERNATIONALIZATION.md)*