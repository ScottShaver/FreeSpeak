# Copilot Instructions

## General Guidelines
- Whenever you create or update a class method, make sure the C# documentation comments are added and correct.
- Whenever you have to make a plan for a prompt, break it into many steps and stop after each step asking me if I want you to continue, so we can avoid token and rate limit issues.
- Whenever you add a NuGet package to the solution, make sure to update the Technology Credits page.
- Do not use PowerShell helper scripts or multi-line terminal commands to create files. Use the create_file tool directly instead.

## Project Overview
- FreeSpeak is a Blazor (.NET 10) social network backed by PostgreSQL and EF Core. Optional Redis caching.
- Projects: `FreeSpeakWeb` (app), `FreeSpeakWeb.Tests` (unit and bUnit), `FreeSpeakWeb.IntegrationTests` (Testcontainers PostgreSQL), `FreeSpeakWeb.PerformanceTests` (BenchmarkDotNet).
- Read the relevant guide in `docs/` before changing an area: `REPOSITORY_PATTERN.md`, `LOCALIZATION.md`, `TESTING.md`, `AUDIT_LOGGING_SYSTEM.md`, `CACHING.md`, `SECURITY.md`, `CONFIGURATION.md`, `THEME_SYSTEM.md`, `DEVELOPER_GUIDE_BASE_COMPONENTS.md`.

## Architecture
- Layering: Razor components -> Services (`Services/`) -> Repository interfaces (`Repositories/Abstractions/`) -> Repositories -> `DbContext`. Services and components must not use `DbContext` directly.
- New data access goes in a repository with an interface, registered in DI. Follow the existing patterns in `Repositories/`.
- Prefer existing base components and helpers (see `DEVELOPER_GUIDE_BASE_COMPONENTS.md`, `Components/Pages/Base/`) over duplicating logic.
- Use DTOs in `DTOs/` for data passed to the UI. Do not expose EF entities where a DTO already exists for that area.
- Settings use the Options pattern. Do not hard-code values that belong in `appsettings.json`.

## Code Style
- Every new or changed class, method and public member needs correct C# XML documentation comments.
- Use async/await end to end. Pass `CancellationToken` where nearby code does.
- Inject `ILogger<T>` and log failures. Do not swallow exceptions silently.
- Keep changes surgical. Do not refactor or reformat unrelated code.

## Localization (required for all user-facing text)
- Never hard-code user-facing strings in `.razor` or `.cs`. Use `IStringLocalizer<T>` with a `.resx` file under `FreeSpeakWeb/Resources/` mirroring the component path.
- Every new key must be added to the neutral `.resx` and to ALL locale files: ar, de, es, fr, it, ja, ko, nl, pl, pt, ru, zh. Keep the `.Designer.cs` file in sync.
- When you touch a `.resx`, check that every locale file has the same keys as the neutral file, and add any that are missing.
- Any new or changed UI component (page, component, modal, toast, service message shown to users) must ship fully localized in the same change: neutral `.resx`, all 12 locale files and `.Designer.cs`, with real translations rather than copied English. Do not defer translations to a follow-up PR or leave English placeholders in locale files.
- Use culture-aware formatting for dates, times and numbers; do not hard-code format strings or English fragments such as "ago" or "just now".
- Follow `docs/LOCALIZATION.md`.

## Database and Migrations
- Schema changes need an EF Core migration in `FreeSpeakWeb/Migrations`. Do not edit existing migrations or the model snapshot by hand.
- Generated migration SQL scripts are never committed.
- Add indexes for new query patterns used on large tables.

## Security and Auditing
- Enforce authorization on the server (services), not only by hiding UI. Check group roles (admin, moderator, member) and system roles.
- Sanitize user-generated HTML with the existing sanitization service. Validate uploads with the existing file validators.
- Record user and admin actions through `IAuditLogRepository` using the matching `ActionCategory` and details class. See `docs/AUDIT_LOGGING_QUICK_REFERENCE.md`.
- Never commit secrets, connection strings or real credentials.

## Testing and Definition of Done
- Add tests for new services and components (xUnit, bUnit, Moq, FluentAssertions; AAA pattern). Put them beside similar tests in `FreeSpeakWeb.Tests`.
- Before finishing, run `dotnet build` and `dotnet test` for the affected projects. Fix any failures you caused.
- UI changes can be verified through the agent-test flow in `docs/AGENT_TESTING.md`. Only use the disposable `FreeSpeak_AgentTest` database. Never point an agent at a dev or production database. Any agent that opens a browser (Playwright) must close all browser windows it opened (`browser_close`) before finishing, even if the test failed.- Update the relevant `docs/` file when behavior changes. Update the Technology Credits page when adding NuGet packages.

## Agent Workflow for New Features
- Feature work is spec-driven. Specs live in `docs/specs/<feature>.md`. Implement one spec per branch and pull request.
- Implement strictly what the spec says. If the spec is ambiguous or incomplete, stop and ask instead of inventing scope.
- Do not mark work complete until every acceptance criterion in the spec is met and the build and tests pass.
- Commit messages: short imperative summary, with details in the body when needed.

## Local database and agent testing
- A local PostgreSQL server runs on localhost:5432. The disposable test database is `FreeSpeak_AgentTest`; see `docs/AGENT_TESTING.md` for the reset and start scripts, test personas and charters.
- Only run `Reset-AgentTestDb.ps1` and `Start-AgentTestApp.ps1` for testing. They touch only `FreeSpeak_AgentTest`.
- Never run migrations, resets, seeders or destructive SQL against any other database, and never change connection strings to point at one.
- Database credentials live outside the repo (the user's `pgpass.conf`). Never read, print, log or commit them.
- Run the scripts with `powershell -ExecutionPolicy Bypass -File ...`. The seeder only runs in Development with `AgentTest:Enabled` and a database name ending `_AgentTest`; do not weaken these guards.
