# Spec: System Moderator Dashboard

Status: APPROVED for implementation. Owner decisions are recorded in section 10.
Source: `gap-analysis-report.md`, gap #3. Depends on `feed-content-reporting.md` (approved), which supplies the reports this dashboard reviews.

## 1. Goal

Turn the placeholder `/system-moderator` page into a working moderation workspace for users in the `SystemModerator` and `SystemAdministrator` roles: review reported feed content, act on it, and see what moderators have done.

## 2. Current State (verified)

- `Components/Pages/SystemModerator.razor` (`@page "/system-moderator"`, `[Authorize(Roles = "SystemAdministrator,SystemModerator")]`, `InteractiveServer`) shows a welcome alert, a static bullet list of "future" functions (content moderation queue, user reports review, flagged posts, guideline enforcement, user warnings and suspensions, moderation activity logs), and the user's own role info. No behavior.
- `NavMenu.razor` already links to the page for both roles.
- Resources: `Resources/Pages/SystemModerator*.resx` (all locales).
- Roles: `RoleType.SystemModerator`, handled by `RoleService`.
- Audit categories `SystemModeratorRemovePost` (26) and `SystemModeratorRemovePostComment` (27) and their details classes exist but are never logged.
- `UserLockoutService` supports permanent and temporary lockouts, but no moderator-facing UI.
- The feed report entity and service do not exist yet (see `feed-content-reporting.md`).
- Group moderation is handled inside each group by group moderators. System moderators currently have no cross-group view.

## 3. Scope and Delivery Phases

- **PR 1 (first PR):** page layout with tabs, overview counts, feed report queue, report detail and actions (dismiss, resolve, remove content), and audit logging of moderator removals.
- **PR 2:** user warnings and suspensions (section 5).
- **PR 3:** system-level backup moderation of group reports across all groups (section 6), plus the system moderation activity log tab.

Remove the placeholder "future functions" list as each part ships.

## 4. PR 1 Design

### 4.1 Page structure

Tabs (Bootstrap nav tabs, same style as `GroupModerationModal`), extracted into child components to keep the page small:
- **Overview:** count cards (pending feed reports, reports under review, resolved in the last 7 days). Counts come from `FeedContentReportService`. Keep the "Your information" card.
- **Feed Reports:** the queue (`Components/Moderation/FeedReportQueue.razor`).
- Tabs for later phases appear only when implemented (no disabled placeholder tabs).

### 4.2 Report queue

- Lists reports with status `NotReviewed` or `UnderReview`, oldest first (configurable sort: newest or oldest), paged (25 per page, "Load more").
- Filters: status, reason, content type (post or comment), date range.
- Each row: reason icon and label, content type, short plain-text excerpt of the content, author display name, reporter display name, time reported (server UTC shown in viewer local time with relative text), status badge, number of other pending reports against the same content.
- Empty state, loading state and error alert, following `GroupModerationModal` patterns.

### 4.3 Report detail and actions

Opening a report shows the full reported post or comment read-only (reuse `PostDetailModal` in read-only mode, as the group moderation modal does), the reporter's description, and prior reports on the same content or against the same author.

Actions (each requires a confirmation, an optional note, and is permission checked in the service):
- **Mark under review:** claims the report (`ReviewerId`).
- **Dismiss:** no violation. The report closes.
- **Resolve:** reviewed, no further action needed.
- **Remove content and take action:** deletes the post or comment through the existing deletion services (so images and related data are cleaned up per `docs/POST_DELETION_CLEANUP.md`), sets the status to `ActionTaken`, and writes `SystemModeratorRemovePost` or `SystemModeratorRemovePostComment` audit entries with reason and the author snapshot. Do not hard-delete in a way that bypasses cleanup.

Removal also sends the content author a neutral in-app notification (content removed for violating community guidelines; no moderator identity, no appeal).

All actions set `ReviewerId` and a server-set UTC `ReviewedAt`. Each triggers the reporter outcome notification defined in `feed-content-reporting.md` (Q2). All other open reports on the same content close together with the same outcome.

### 4.4 Service layer

- `SystemModerationService` (`Services/`) orchestrates the actions above, calling `FeedContentReportService`, the post and comment services and `IAuditLogRepository`. UI components never reach into repositories.
- Role check in the service: caller must be in `SystemModerator` or `SystemAdministrator`. Returns a failure result (not an exception) if not.
- A moderator cannot act on a report about their own content, and cannot review a report they filed. Another moderator or admin must handle it.
- Concurrency: if another moderator already closed the report, the action fails with a clear message and the queue refreshes (use the existing concurrency pattern or a status check in the update).

## 5. PR 2 Design (user warnings and suspensions)

- New `UserWarning` entity: `Id`, `UserId`, `IssuedByUserId`, `Reason`, `RelatedReportId?`, `CreatedAt` (server UTC), `AcknowledgedAt?`. Needs a migration.
- Warn action from the report detail and from a user lookup: records the warning and sends the user an in-app notification.
- Suspend action reuses `UserLockoutService`. Records who suspended, why, and until when (server-set UTC timestamps). Suspended users see a clear, localized message at login.
- Limits enforced in the service, not the UI:
  - `SystemModerator`: can warn and suspend temporarily, maximum 7 days (setting `Moderation:MaxModeratorSuspensionDays`, default 7).
  - `SystemAdministrator`: can also suspend for longer periods or permanently, and can lift any suspension.
  - A moderator cannot suspend or warn another system moderator or administrator, or themselves.
- Audit categories (new): warn, suspend, unsuspend, with details classes, per `docs/AUDIT_LOGGING_SYSTEM.md`.
- A user history panel (warnings, suspensions, past reports) for moderators.
- Appeals are out of scope.

## 6. PR 3 Design (group reports backup and activity log)

System moderators act as a backstop for group moderation when group moderators fail to act. Groups remain self-managed first.

- **Group Reports tab:** lists `GroupContentReport` records across all groups with the same queue UX as feed reports. Filters: group, status, reason, and "pending longer than N days". Default view assumption: show all pending group reports, oldest first, with a quick filter for stale ones (default N = 3, configurable). See Q7.
- Actions on group reports by a system moderator: mark under review, dismiss, resolve, remove content (group post or comment), and the warn or suspend actions from section 5.
- **Required service changes:** the existing group services check group roles (admin or moderator). Add an explicit system-level override for `SystemModerator` and `SystemAdministrator` in the group report, post, comment and file services. Keep the checks in the services, not the UI. This also covers the existing TODO in `GroupFileService.cs` ~L460 ("Check system-level admin/moderator roles if needed").
- A system moderator action on a group report must:
  - write the global audit entry (`SystemModeratorRemovePost` or `SystemModeratorRemovePostComment`, including `GroupId`);
  - create a Group Mod Log entry (see `group-moderation-log.md`) clearly marked as a system-level action, so group moderators can see it;
  - notify the reporter and the author, as for feed content.
- If a group moderator and a system moderator act on the same report at the same time, the second action fails safely with a clear message.
- **Moderation Activity tab:** lists system moderator audit entries (categories 26, 27 and the warn and suspend categories) with filters by moderator, action and date, paged, with CSV export mirroring `group-moderation-log.md`. Reuse `IAuditLogRepository.SearchAuditLogsAsync`. Every entry shows its server-set UTC timestamp.

## 7. Permissions

- Page access: `SystemModerator` and `SystemAdministrator` only (already in place). Keep it.
- All reads and actions are also enforced in services, not only by hiding UI.
- Regular users, group admins and group moderators get no access.
- Never expose reporter identity to anyone except system moderators and administrators.

## 8. Localization

All new text in `Resources/Pages/SystemModerator*.resx` and new resource sets for each new component (`Resources/Moderation/...`), with the neutral file and all 12 locales (ar, de, es, fr, it, ja, ko, nl, pl, pt, ru, zh) and Designer files. Remove obsolete keys of the placeholder list from all locales when they are no longer used.

## 9. Tests and Acceptance Criteria

Tests (`FreeSpeakWeb.Tests`, xUnit, bUnit, Moq, FluentAssertions, AAA):
- `SystemModerationService`: each action succeeds for moderator and admin, is refused for ordinary users, group admins and group moderators; cannot act on own content or own report; double-processing is handled; removal calls the normal deletion path; audit entries (26 and 27) written with timestamps; reporter notified.
- Group services: system moderators and administrators pass the permission checks for group report, post, comment and file actions; ordinary members and non-members still fail. System actions on group reports write the audit and group Mod Log entries.
- Warnings and suspensions: moderator limit of 7 days enforced, administrators unrestricted, no action against other moderators or admins or self, suspended users are blocked at login with the right message, `UserWarning` timestamps server-set.
- bUnit: overview counts, queue filtering and paging, empty state, detail view, confirmation flow, error display.
- Integration: queue query performance and index use on `(Status, CreatedAt)`.

PR 1 acceptance criteria:
1. The placeholder text is gone. The Overview shows live counts.
2. Moderators see a paged, filterable queue of pending feed reports.
3. A moderator can dismiss, resolve, or remove content, each with an optional note.
4. Removal uses the normal deletion path and leaves no orphans.
5. Every action records reviewer and a server-set UTC timestamp, writes an audit entry, and notifies the reporter.
6. Non-moderators cannot reach the page or call the service.
7. Moderators cannot act on their own content or reports.
8. All strings localized in all locales. `dotnet build` and `dotnet test` pass. XML docs and docs updated (`AUDIT_LOGGING_SYSTEM.md`, README feature list if applicable).

PR 2 and PR 3 acceptance criteria:
9. (PR 2) Moderators can warn and suspend up to 7 days. Only administrators can suspend longer. Users are notified, and suspended users see why at login.
10. (PR 3) System moderators can review and act on group reports in any group. Group moderators see those actions in their group Mod Log.
11. (PR 3) The activity tab lists system moderation actions with server-set timestamps and supports CSV export.
12. (PR 1 for feed reports, PR 3 for group reports) A visible message appears when reports have been pending longer than the configured number of days (default 3), with a stale count and a one-click filter. Tests cover the threshold boundary.

## 10. Decisions (owner)

- **Q1 Warnings and suspensions:** yes, included (PR 2).
- **Q2 Suspension authority:** moderators can warn and suspend for up to 7 days. Only administrators can suspend permanently.
- **Q3 Group reports:** system moderators can act on group reports across all groups, as a backup when group moderators fail to act. Groups stay self-managed first (PR 3).
- **Q4 Author notice:** yes, the author gets a neutral notification when content is removed.
- **Q5 Appeals:** out of scope.
- **Q6 Queue sort:** oldest first.
- **Q7 Group report default view:** show all pending reports. In addition, display a prominent, localized banner/message when reports have been pending longer than N days (default 3, setting `Moderation:StaleReportDays`), for example "5 reports have been waiting more than 3 days". The banner shows on the Overview tab and at the top of the Group Reports tab, with a link or button that applies the "pending longer than N days" filter. The Overview also shows a stale count card. The same stale banner applies to the feed report queue.

## 11. Suggested Task Breakdown (PR 1)

1. `SystemModerationService`, role checks, actions, audit logging, with tests.
2. Overview counts and the tabbed page shell, with localization.
3. Report queue component, filters and paging, with tests.
4. Report detail view and action dialogs, reporter and author notifications, with tests.
5. Remove placeholder list and obsolete resources in all locales, docs, final build and test run.
