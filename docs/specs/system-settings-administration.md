# Spec: System Settings Administration

Status: Approved.
Gap reference: the disabled "Site Settings", "Security Settings", "Storage Settings", "Performance" and
"Email Configuration" cards on `SystemAdmin.razor`.
Relationship to `email-settings.md`: this spec generalizes it. The Smtp group below is delivered by
the email-settings spec's page and test-email feature, but stored through the generic mechanism here.
Build this spec first (PR 1), then email settings becomes the first group built on it.

## 1. Goal

A SystemAdministrator can view and change every operational setting currently in `appsettings.json`
from the System Admin area, grouped by topic, without editing files. Changes apply without a restart
wherever the consuming code allows it; otherwise the UI says "restart required".

## 2. Current state (verified)

Settings are read through `IOptions<T>` (bound once at startup) from `appsettings.json`:
`SiteSettings` (root keys), `Smtp`, `VirusScan`, `RateLimiting`, `Profiling`, `Caching`, `Logging`,
`SystemAdministratorInitInfo`, `ConnectionStrings`, `AllowedHosts`, plus the `UseIdentityNoOpEmailSender`
toggle. `Program.cs` binds them (~L66-157, L242). No UI edits any of them.

## 3. Setting groups

| Group | Keys | Editable in UI | Applies |
|---|---|---|---|
| Site | SiteName, MaxFeedPostCommentDepth, MaxFeedPostDirectCommentCount, AllowOpenGroupCreation, AllowPostAttachments | Yes | Live |
| Smtp (Email) | Server, Port, SenderName, SenderEmail, AuthenticationRequired, Account, Password, UseSsl, Enabled (UseIdentityNoOpEmailSender) | Yes | Live |
| VirusScan | Enabled, ClamAvHost, ClamAvPort, MaxStreamSizeBytes, FailOpen, TimeoutSeconds | Yes | Live |
| RateLimiting | UseDistributed; Global, FileDownload, Login, Api each with MaxRequests, WindowMinutes, QueueLimit | Yes | Restart required (limiter policies are built at startup) |
| Profiling | Enabled, ShowControls, MaxResults, ProfileDatabase, AllowOutsideDevelopment | Yes | Restart required |
| Caching | UseRedis, RedisConnectionString | Yes (connection string is a secret) | Restart required |
| Logging | LogLevel for a fixed list of standard categories (see section 9) | Yes | Live (via `LoggerFilterOptions` reload) |
| Moderation | StaleReportDays and other settings introduced by the moderation specs | Yes | Live |

Not editable in the UI (bootstrap/secret, remain file/environment only, shown read-only where useful):
`ConnectionStrings` (needed to reach the settings table at all), `SystemAdministratorInitInfo`
(should be removed after first run), `AllowedHosts` (security-sensitive, restart required, lockout risk).

## 4. Design

### 4.1 Storage
- Table `SystemSettings`: `Key` (PK, e.g. `VirusScan:ClamAvHost`), `Value` (text), `IsSecret` (bool),
  `UpdatedAtUtc`, `UpdatedByUserId`. Secret values (Smtp password, Redis connection string) are
  encrypted with ASP.NET Core Data Protection and never returned to the UI (write-only fields).
- Effective value = database row if present, otherwise `appsettings.json` / environment value.
  "Reset to default" deletes the row.
- Values keep the same key paths as `appsettings.json`, so existing `IOptions<T>` binding is unchanged.

### 4.2 Configuration integration
- A custom `IConfigurationSource`/provider (`DatabaseConfigurationProvider`) added after the JSON
  sources in `Program.cs`, so database rows override file values. It uses the `DefaultConnection`
  string directly via Npgsql (no DbContext) and tolerates a missing table (first run before migration).
- `SettingsService.SaveAsync` writes rows then calls `Reload()` on the provider. Consumers that need
  live values move from `IOptions<T>` to `IOptionsMonitor<T>` (SiteSettings, SmtpSettings,
  VirusScanSettings, Moderation settings). Restart-required groups stay on `IOptions<T>`.
- Each group declares `RestartRequired` metadata; the UI shows a banner after saving such a group.
- Multi-instance deployments: the provider re-polls every 60 seconds so other instances pick up changes.

### 4.3 Service layer
- `ISystemSettingsRepository` / `SystemSettingsRepository`: get all, upsert many, delete.
- `ISystemSettingsService` / `SystemSettingsService`: `GetGroupAsync(group)`, `SaveGroupAsync(group, dto,
  adminUserId)`, `ResetGroupAsync`, per-group validation, audit, role check.
- Group definitions are declarative (key, type, min/max, secret, restart-required) so the page is
  rendered from metadata and new settings need no new UI code.

### 4.4 UI
- Enable the System Admin cards and add `/system-admin/settings/{group}` pages (SystemAdministrator only):
  a group list with a field form per group, validation messages, "Reset to default", a "Restart
  required" notice, and a "Using default from appsettings.json" indicator per field.
- Group-specific actions: Smtp "Send test email" (per `email-settings.md`, 5 per admin per hour);
  VirusScan "Test connection" (ClamAV PING).
- Secret fields show "set / not set" and a replace box only.

### 4.5 Validation (server-side, per group)
- Ports 1-65535; positive integers for limits; `WindowMinutes` >= 1; `MaxStreamSizeBytes` > 0;
  valid email for `SenderEmail`; valid log level names; host strings non-empty.
- Safety guard: Login rate limit cannot be saved below 3 requests, and Global cannot be 0, to avoid
  locking everyone out.

## 5. Permissions and audit
- SystemAdministrator only; System Moderators get 403. Service re-checks the role.
- New `ActionCategory` values `SystemSettingChanged` and `SystemSettingReset` with a `*Details` class
  (group, key, old value, new value; secrets recorded as "changed" only, never the value).
  Server-set UTC timestamp on every entry (see `docs/AUDIT_LOGGING_SYSTEM.md`).

## 6. Localization
- Group names, field labels, help text, validation and banner text in all 12 locales with identical keys
  (ar, de, es, fr, it, ja, ko, nl, pl, pt, ru, zh), plus updated `.Designer.cs`.

## 7. Tests
- Unit: provider override precedence, reload, missing-table tolerance, secret encryption round-trip,
  validation per group, safety guards, reset deletes the row, audit never contains secrets,
  authorization checks.
- Unit: consumers using `IOptionsMonitor` see updated values after save.
- bUnit: forms render from metadata, secrets are never shown, restart banner appears for restart groups.
- Integration (Testcontainers): table migration, provider reads and reloads, multi-instance poll.

## 8. Acceptance criteria
1. Every key in the section 3 table (except the non-editable ones) can be viewed and changed in the UI.
2. Live groups take effect without restart; restart-required groups show a notice.
3. Secrets are encrypted at rest and never displayed or logged.
4. With an empty table the app behaves exactly as before using `appsettings.json`.
5. Reset to default restores the file value.
6. Lockout-prone values are rejected by validation.
7. Changes are audited with a UTC timestamp; non-administrators are denied.
8. All strings exist in all 12 locales; build and tests pass.
9. The Effective configuration page requires password re-authentication and always masks secrets.
10. Logging accepts only the fixed category list and valid levels.

## 9. Decisions (resolved)
1. The database overrides the file for all editable groups.
2. `ConnectionStrings`, `SystemAdministratorInitInfo` and `AllowedHosts` stay file-only.
3. A read-only "Effective configuration" page shows those non-editable keys with secrets masked.
   It is password protected: the administrator must re-enter their own account password to open it
   (re-authentication, valid for 5 minutes, failed attempts audited and rate limited). The page
   never reveals secret values even after re-authentication; passwords and connection-string
   credentials stay masked.
4. The Logging group is limited to a fixed list of standard ASP.NET categories: `Default`,
   `Microsoft`, `Microsoft.AspNetCore`, `Microsoft.Hosting.Lifetime`,
   `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Database.Command`, and `System`.
   Each takes a standard level (Trace, Debug, Information, Warning, Error, Critical, None).
   No arbitrary category names.

## 10. Task breakdown
1. `SystemSettings` entity, migration, repository, Data Protection encryption.
2. `DatabaseConfigurationProvider` and `Program.cs` wiring.
3. Settings service, group metadata, validation, audit categories.
4. Settings pages and System Admin card wiring.
5. Move live-group consumers to `IOptionsMonitor`.
6. Smtp group: test email and `SmtpEmailSender` reads live settings (per `email-settings.md`).
7. VirusScan test-connection action.
8. Effective configuration read-only page with password re-authentication and masking.
9. Localization for 12 locales.
10. Unit, bUnit and integration tests (include re-auth expiry, failed-attempt audit, masking).
