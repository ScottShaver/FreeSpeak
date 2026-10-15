# Spec: Email (SMTP) Settings Administration

Status: Approved.
Gap reference: #5 in the gap analysis (corrected: the sender exists; the admin UI is missing).

## 1. Goal

Let a System Administrator view and change the SMTP configuration from the System Admin page,
and send a test email, without editing `appsettings.json` or restarting the app.

## 2. Current state (verified)

- `Components/Account/SmtpEmailSender.cs` is a working MailKit sender. It is a singleton
  `IEmailSender<ApplicationUser>` bound to `IOptions<SmtpSettings>` at startup.
- `Program.cs` (~L106-112): `UseIdentityNoOpEmailSender = true` registers `IdentityNoOpEmailSender`
  (log only); otherwise `SmtpEmailSender`.
- `appsettings.json` has a `Smtp` section: `Server`, `Port`, `SenderName`, `SenderEmail`,
  `AuthenticationRequired`, `Account`, `Password`, `UseSsl` (placeholder values).
- `SystemAdmin.razor` (~L274) has a disabled "Email Configuration" card marked "Coming Soon".

## 3. Design

### 3.1 Storage
- New table `EmailSettings` (single row, `Id = 1`): `Server`, `Port`, `SenderName`, `SenderEmail`,
  `AuthenticationRequired`, `Account`, `EncryptedPassword`, `UseSsl`, `Enabled`,
  `UpdatedAtUtc`, `UpdatedByUserId`.
- Password is encrypted with ASP.NET Core Data Protection (purpose string `EmailSettings.Password`).
  It is never returned to the UI; the field is write-only ("leave blank to keep current").
- Effective settings = database row if present, otherwise `appsettings.json` `Smtp` values.
- Migration is generated with `dotnet ef`; SQL scripts are not committed.

### 3.2 Service layer
- `IEmailSettingsRepository` / `EmailSettingsRepository`: get and upsert the row.
- `IEmailSettingsService` / `EmailSettingsService`:
  - `GetEffectiveSettingsAsync()` returns decrypted settings (internal use only).
  - `GetForEditAsync()` returns a DTO with `HasPassword` instead of the password.
  - `SaveAsync(dto, adminUserId)` validates, encrypts, persists, invalidates the cache, audits.
  - `SendTestEmailAsync(recipient, adminUserId)` sends a test message and returns success/error text.
  - Short in-memory cache (invalidated on save) so sends do not hit the database every time.
- `SmtpEmailSender` is changed to read `IEmailSettingsService.GetEffectiveSettingsAsync()` per send
  instead of `IOptions<SmtpSettings>`. Registered so the singleton can resolve the service safely
  (use `IServiceScopeFactory` or make the settings service a singleton with its own scope).
- Sender selection: keep `UseIdentityNoOpEmailSender` as the startup default, but when `Enabled`
  is false in the database the sender logs and skips sending instead of throwing.

### 3.3 UI
- Enable the "Email Configuration" card on `SystemAdmin.razor`, routing to `/system-admin/settings/smtp` (the generic settings group page from the system settings administration spec).
- New page `EmailSettings.razor` (`[Authorize(Roles = "SystemAdministrator")]`) with fields for all
  settings, an Enabled toggle, a write-only password field, a "Send test email" input and button,
  and inline validation messages.
- Test button shows success, or the sanitized error message (never the password or full stack trace).

### 3.4 Validation
- Server required, port 1-65535, sender email valid format, sender name max 100 chars.
- Account required when authentication is required.
- Test email recipient must be a valid email address.

## 4. Permissions
- SystemAdministrator only. System Moderators and all other roles get 403 / redirect.
- Service methods re-check the caller's role; UI hiding is not sufficient.

## 5. Audit
- Add `ActionCategory` values for `EmailSettingsChanged` and `EmailTestSent` (follow
  `docs/AUDIT_LOGGING_SYSTEM.md`) with `*Details` classes.
- Change details list which fields changed and whether the password changed. They never contain
  the password value. All entries carry a server-set UTC timestamp.

## 6. Localization
- New resource set for the page and card text in all 12 locales (ar, de, es, fr, it, ja, ko, nl,
  pl, pt, ru, zh) with identical keys; update the `.Designer.cs` file.

## 7. Tests
- Unit: service validation, effective-settings precedence (DB over config), password encryption
  round-trip, blank password keeps existing, audit never contains the password, authorization checks.
- Unit: `SmtpEmailSender` uses current settings after a save, and skips when disabled.
- bUnit: page renders fields, hides the password, shows validation errors, test button states.
- Integration: repository upsert and migration against Testcontainers PostgreSQL.

## 8. Acceptance criteria
1. A SystemAdministrator can open Email Configuration from the System Admin page.
2. Saved changes take effect on the next email without an app restart.
3. The password is stored encrypted and is never displayed or logged.
4. With no database row, the app behaves exactly as before using `appsettings.json`.
5. The test email button reports success or a clear failure reason.
6. Changes and test sends are audited with a UTC timestamp.
7. Non-administrators cannot view or call the settings.
8. All strings exist in all 12 locales; build and tests pass.

## 9. Decisions (resolved)
1. Email templates are out of scope for this spec.
2. The test email is limited to 5 per administrator per hour.
3. An Enabled toggle is included in addition to `UseIdentityNoOpEmailSender`.

Manual verification uses a real SMTP account (Mailgun). Credentials are entered through the
admin UI at test time and must never be committed to the repository or appear in logs.

## 10. Task breakdown
1. Entity, migration, repository, and Data Protection encryption.
2. Settings service, caching, validation, and audit categories.
3. Update `SmtpEmailSender` to use the service.
4. `EmailSettings.razor` page and enabling the System Admin card.
5. Localization for 12 locales.
6. Unit, bUnit, and integration tests.
