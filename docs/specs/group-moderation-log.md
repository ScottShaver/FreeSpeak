# Spec: Group Moderation Log (Mod Log)

Status: APPROVED for implementation. Owner decisions are recorded in section 10.
Source: `gap-analysis-report.md`, gap #1.

## 1. Goal

Replace the placeholder "Moderation Log" tab in `Components/Groups/GroupModerationModal.razor` with a real, per-group, read-only log of moderation actions. Group moderators and admins can see who did what, to which content or member, when, and why.

## 2. Current State (verified)

- The `modlog` tab renders only `Localizer["ModerationLogPlaceholder"]` plus the comment `<!-- Future: Log of moderation actions taken -->`.
- CSS for `.modlog-entry`, `.modlog-time` and `.modlog-action` already exists in `GroupModerationModal.razor.css`.
- Resource keys `ModerationLog`, `ModerationLogDescription` and `ModerationLogPlaceholder` exist in `Resources/Groups/GroupModerationModal*.resx` (all locales).
- The global `AuditLog` table exists (`Data/AuditLog.cs`, `IAuditLogRepository`, `AuditLogDetails/*`). It is monthly-partitioned and stores `ActionDetails` as a JSON `TEXT` column.
- `ActionCategory` already defines `GroupModeratorApprovePost` (22), `GroupModeratorDeclinePost` (23), `GroupModeratorRemovePost` (24) and `GroupModeratorRemovePostComment` (25), with matching `*Details` classes.
- **These moderator categories are never logged.** No service calls `LogActionAsync` with them.
- `GroupContentReportService` and `GroupFileService` write no audit entries at all.
- `GroupBannedMemberService.BanUserAsync` logs `GroupAdminBanUser`. `UnbanUserAsync` logs nothing.
- Group admin actions (edit group, join request approve or deny, role change) are logged to `AuditLog` with `GroupId` inside the JSON.

Consequence: the work is not only a UI. Moderation events must first be recorded.

## 3. Design Decision: Storage

Recommended: a new dedicated table `GroupModerationLogEntries`, written in addition to the existing global `AuditLog` entries.

Why not just query `AuditLog`:
- `GroupId` lives inside a JSON `TEXT` column. Per-group filtering would need text matching on a partitioned table, which is slow and fragile.
- The Mod Log needs a stable, indexed, per-group shape that is independent of audit retention and partitioning.

The global `AuditLog` remains the system audit trail. Both writes happen in the same service method.

## 4. Data Model

Entity `GroupModerationLogEntry` (`FreeSpeakWeb/Data/GroupModerationLogEntry.cs`):

| Field | Type | Notes |
|-------|------|-------|
| `Id` | long | PK |
| `GroupId` | int | Required. Indexed. FK to Group, cascade delete |
| `ActorUserId` | string(450) | Moderator or admin who acted. No FK, so history survives user deletion |
| `ActorDisplayName` | string(200) | Snapshot at time of action |
| `Action` | `GroupModerationAction` enum (stored as int) | See below |
| `TargetType` | `GroupModerationTargetType` enum | Post, Comment, File, Member, Report, Group |
| `TargetId` | string(450)? | Id of the target entity |
| `TargetUserId` | string(450)? | Affected member or content author |
| `TargetUserDisplayName` | string(200)? | Snapshot |
| `Summary` | string(500)? | Short content excerpt or file name, plain text, truncated |
| `Reason` | string(1000)? | Moderator-entered reason or notes |
| `CreatedAt` | DateTime (UTC) | Required, non-null. Set by the server at the moment the action succeeds (`DateTime.UtcNow`), never by the client or caller. Every logged action must have it |

Indexes: `(GroupId, CreatedAt DESC)` as the primary query path. `(GroupId, Action, CreatedAt DESC)` for filtering.

`GroupModerationAction` enum values:
`PostApproved`, `PostDeclined`, `PostRemoved`, `CommentRemoved`, `FileApproved`, `FileDeclined`, `FileRemoved`, `ReportDismissed`, `ReportResolved`, `MemberBanned`, `MemberUnbanned`, `MemberRemoved`, `MemberRoleChanged`, `JoinRequestApproved`, `JoinRequestDenied`, `GroupSettingsChanged`, `RuleAdded`, `RuleEdited`, `RuleDeleted`.

Delivery tiers (decided in section 10):
- **PR 1 (first PR):** post approve or decline, report dismiss or resolve, file approve or decline, ban and unban, plus the table, UI, 6-month retention purge and CSV export.
- **PR 2 (follow-up, same feature):** post or comment removal by moderators, member removal, role changes, join requests, group settings changes, and rule add, edit or delete. These must appear in the Mod Log once PR 2 lands.

A migration is required. Do not edit existing migrations. Do not commit generated SQL scripts.

## 5. Services and Repositories

- `IGroupModerationLogRepository` (`Repositories/Abstractions/`) and `GroupModerationLogRepository`:
  - `AddAsync(GroupModerationLogEntry entry)`
  - `GetPagedAsync(int groupId, GroupModerationLogFilter filter, int pageNumber, int pageSize)` returning items plus total count.
- `GroupModerationLogService` (`Services/`):
  - `LogAsync(...)`: builds and saves an entry. It must never throw into the caller. Log failures with `ILogger` and continue, so moderation actions never fail because logging failed.
  - `GetLogAsync(int groupId, string requestingUserId, GroupModerationLogFilter filter, int page, int pageSize)`: server-side authorization (see section 7).
- Register both in DI.
- DTO `GroupModerationLogEntryDto` in `DTOs/`. The UI must not bind to the entity.
- `GroupModerationLogFilter`: optional `Action`, `ActorUserId`, `FromDate`, `ToDate`.

### Hook points (call `LogAsync` after the action succeeds)

| Action | Method |
|--------|--------|
| PostApproved / PostDeclined | `GroupPostService.ApprovePostAsync` / `DeclinePostAsync` |
| ReportDismissed / ReportResolved | `GroupContentReportService.DismissReportAsync` / `ResolveReportAsync` / `MarkActionTakenAsync` |
| FileApproved / FileDeclined / FileRemoved | `GroupFileService.ApproveFileAsync` / `DeclineFileAsync` / `DeleteFileAsync` (only when the deleter is not the uploader) |
| MemberBanned / MemberUnbanned | `GroupBannedMemberService.BanUserAsync` / `UnbanUserAsync` |
| PostRemoved / CommentRemoved | `GroupPostService.DeleteGroupPostAsync` / `DeleteCommentAsync` (only when actor is not the author) |
| PR 2 items | `GroupMemberService` join request, role and remove-member methods; `GroupService` settings update; `GroupRuleService` add, edit and delete |

Also: write the matching global `AuditLog` entries for categories 22-25 using the existing `GroupModerator*Details` classes, and add an unban audit category and details class following `docs/AUDIT_LOGGING_SYSTEM.md` ("Adding New Audit Categories"). Update `AUDIT_LOGGING_SYSTEM.md` and the quick reference.

Hook the action where the moderator acts, not in a repository, so the actor id is available.

## 6. UI

In the `modlog` tab of `GroupModerationModal.razor`:

- Heading with `ModerationLogDescription`.
- Filter bar: action dropdown, date range. (Actor filter is optional.)
- Entry list, newest first, using the existing `.modlog-entry`, `.modlog-time` and `.modlog-action` classes. Each entry shows an action icon, a localized action text, the actor, the target, the optional summary and reason, and a relative time with the absolute time as a tooltip.
- Paging: "Load more" button (page size 25). Do not load everything.
- States: loading spinner, empty state ("No moderation actions yet"), error alert.
- Load lazily when the tab is first opened, as the other tabs do.
- Keep the code-behind consistent with existing patterns in the modal. If the modal grows too large, extract a `ModerationLogTab.razor` component, as `BannedMembersTab` and `GroupRulesTab` do.
- The existing placeholder resource key is no longer used. Remove it from all locale files and the Designer file, or leave it and note why.

## 6a. Retention (PR 1)

- Entries older than 6 months are purged.
- Add a `GroupModerationLogCleanupService` hosted background service, following the `NotificationCleanupService` pattern (see `docs/NOTIFICATION_CLEANUP.md`) and registered next to it in `Program.cs`. Run daily and delete in batches.
- The retention period is a setting in `appsettings.json` (for example `GroupModerationLog:RetentionMonths`, default `6`), bound with the Options pattern and documented in `docs/CONFIGURATION.md`.
- The global `AuditLog` keeps its own retention. It is not affected.
- Show a short note in the tab that entries are kept for 6 months (localized, using the configured value).

## 6b. CSV Export (PR 1)

- An "Export CSV" button in the tab exports the entries matching the current filters (date range, action).
- Same permission check as viewing, enforced in the service. Cap the export (for example 10,000 rows) and say so in the UI if truncated.
- Columns: timestamp (UTC, ISO 8601), actor, action, target type, target, target user, summary, reason.
- Protect against CSV injection: prefix any cell starting with `=`, `+`, `-`, `@`, tab or carriage return with a single quote, and quote or escape commas, quotes and newlines correctly.
- Deliver through the existing download pattern in the app (check how other downloads work first). Do not write temp files containing member data to disk.
- Record each export as a global `AuditLog` entry (who exported which group, when).

## 7. Permissions

- Only group moderators, group admins and system administrators may read a group's log. Enforce in `GroupModerationLogService.GetLogAsync`, not only in the UI.
- Regular group members and non-members can never view or export the log.
- The log is read-only. No edit or delete from the UI.
- Never include data for content the viewer could not already see in the moderation modal.

## 8. Localization

New strings (action names, filter labels, empty state, load more, error) go in a new or the existing `GroupModerationModal` resource set (or `ModerationLogTab` if extracted). Add every key to the neutral file and all 12 locales (ar, de, es, fr, it, ja, ko, nl, pl, pt, ru, zh), and update the `.Designer.cs`. User-entered reasons are shown as written and are never translated.

## 9. Tests and Acceptance Criteria

Tests (`FreeSpeakWeb.Tests`, xUnit + Moq + FluentAssertions, AAA):
- `GroupModerationLogServiceTests`: logging persists an entry; logging failure does not throw; unauthorized user (plain member, non-member) gets nothing; moderator and admin succeed; filtering and paging work.
- Service tests for each hooked method: success creates exactly one entry with the right action, actor and target. Failure creates none.
- bUnit test for the tab: renders entries, empty state, load more, filter, export button.
- Cleanup service test: entries older than the retention period are deleted, newer ones kept.
- CSV export tests: correct columns, escaping, formula-injection prefixing, row cap, authorization.
- Integration test (Testcontainers) for the repository query and index usage on `(GroupId, CreatedAt)`.

Acceptance criteria:
1. Approving or declining a pending post creates a visible Mod Log entry.
2. Dismissing or resolving a report creates a visible entry.
3. Approving or declining a file creates a visible entry.
4. Banning and unbanning a member create entries.
5. The Mod Log tab lists entries newest first, with filters and "Load more".
6. Empty groups show the empty state.
7. A normal member cannot retrieve the log through the service layer.
8. A failure in logging never causes the moderation action itself to fail.
9. Entries remain after the actor or target user is deleted, until purged by retention.
10. Entries older than 6 months are purged automatically.
11. Moderators and admins can export the filtered log as CSV, safely escaped. Members cannot.
12. All new strings exist in all locales. `dotnet build` and `dotnet test` pass.
13. XML doc comments on all new classes and methods. Docs updated.
14. (PR 2) Group settings changes, rule changes, member removal, role changes, join request decisions and moderator removals also appear in the log.
15. Every Mod Log entry, and every matching global `AuditLog` entry (`ActionStamp`), has a UTC timestamp set by the server. The UI shows it for every entry (relative time with the absolute time as a tooltip, in the viewer's local time) and the CSV export includes it. A test must verify that every hooked action produces an entry with a non-default timestamp.

## 10. Decisions (owner, resolved)

- **Q1 First PR scope:** post, report and file decisions plus ban and unban. Remaining actions in PR 2.
- **Q2 Storage:** dedicated `GroupModerationLogEntries` table.
- **Q3 Admin changes:** group settings and rule changes also appear in the Mod Log (delivered in PR 2).
- **Q4 Retention:** purge after 6 months (configurable, default 6).
- **Q5 Visibility:** members cannot view the log. Moderators, group admins and system administrators only.
- **Q6 Export:** CSV export is wanted.

## 11. Suggested Task Breakdown (for the implementing agent)

1. Entity, enums, DbContext config, migration.
2. Repository, service, DTO, DI registration, plus tests.
3. Hook the Must-have actions and add the audit categories 22-25 and unban, with tests.
4. UI tab, with localization in all locales.
5. Retention cleanup service and setting, with tests.
6. CSV export with tests.
7. Tests for the UI, docs, and final build and test run. (End of PR 1.)
8. (PR 2) Remaining actions listed in section 4, with tests and docs.
