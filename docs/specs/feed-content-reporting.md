# Spec: Feed Post and Comment Reporting

Status: APPROVED for implementation. Owner decisions are recorded in section 10.
Source: `gap-analysis-report.md`, gap #2. Pairs with `system-moderator-dashboard.md` (gap #3), which provides the review UI.

## 1. Goal

Let users report feed posts and feed comments (on the home feed, friend pages and single post pages) the same way they can already report group content. Reports are stored and handed to system moderators for review.

## 2. Current State (verified)

- Group reporting is complete: `GroupContentReport` entity, `GroupContentReportService`, `Components/Groups/ReportContentModal.razor`, and review in `GroupModerationModal`.
- Feed reporting is stubbed:
  - `Components/SocialFeed/UnifiedArticle.razor.cs` ~L422: feed post "Report Post" calls `alert("Report Post - Coming Soon")`.
  - `Components/Pages/Home.razor` ~L1357: `ReportCommentAsync` override is a TODO placeholder.
  - `Components/Pages/Base/PostPageBase.cs` ~L617-633: `HandleReportComment` shows "Report Comment - Coming Soon". It already names the intended pieces: `FeedContentReport`, `FeedContentReportService`, `ReportFeedContentModal`.
  - TODO comments also in `Pages/FriendDetails.razor` ~L401 and `Pages/SinglePost.razor` ~L367.
  - `Pages/SingleGroupPost.razor` ~L576 has the same TODO for group comment reporting. Verify whether group comment reporting is wired there and fix it if it is not (small, tightly related).
- Feed content entities are `Post` and `Comment`. Group equivalents are `GroupPost` and `GroupPostComment`.
- Enums `ReportReason` and `ReportStatus` already exist. `ReportReason.ViolatesGroupRule` and `OffTopic` are group-specific.
- System-level roles exist (`SystemAdministrator`, `SystemModerator`). Audit categories `SystemModeratorRemovePost` (26) and `SystemModeratorRemovePostComment` (27) exist but are not logged anywhere.

## 3. Data Model

New entity `FeedContentReport` (`FreeSpeakWeb/Data/FeedContentReport.cs`), modeled on `GroupContentReport`:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `PostId` | int? | Reported feed post. Null for comment reports |
| `CommentId` | int? | Reported comment. Null for post reports |
| `ContentAuthorId` | string(450) | Snapshot of the author, for review and repeat-offender lookup |
| `ReporterId` | string(450) | FK to user |
| `Reason` | `ReportReason` | Feed reports must not use `ViolatesGroupRule` |
| `Description` | string(2000)? | Optional reporter context |
| `Status` | `ReportStatus` | Default `NotReviewed` |
| `CreatedAt` | DateTime (UTC) | Server-set, required |
| `ReviewerId` | string(450)? | |
| `ReviewedAt` | DateTime? (UTC) | Server-set when status changes |
| `ReviewerNotes` | string(2000)? | |

Constraints and indexes:
- Check constraint: exactly one of `PostId` or `CommentId` is set.
- Index `(Status, CreatedAt DESC)` for the moderator queue. Index `(PostId)`, `(CommentId)`. Unique index on `(ReporterId, PostId)` and `(ReporterId, CommentId)` (filtered where not null) so one user cannot report the same content twice.
- Deleting a post or comment: cascade or mark the report as `Resolved` with a note, so no orphaned reports remain (see `docs/POST_DELETION_CLEANUP.md`; deleting content must clean up its reports).

A migration is required. Do not edit existing migrations. Do not commit generated SQL scripts.

## 4. Services and Repositories

- `IFeedContentReportRepository` / `FeedContentReportRepository` (`Repositories/`), following `docs/REPOSITORY_PATTERN.md`.
- `FeedContentReportService` (`Services/`), mirroring `GroupContentReportService`:
  - `ReportPostAsync(postId, reporterId, reason, description)`
  - `ReportCommentAsync(commentId, reporterId, reason, description)`
  - `GetPendingReportsAsync(requestingUserId, page, pageSize)` and `GetPendingReportCountAsync(requestingUserId)` (system moderators or admins only)
  - `GetReportByIdAsync`, `DismissReportAsync`, `MarkActionTakenAsync`, `ResolveReportAsync`, each recording reviewer and `ReviewedAt`
  - `HasUserReportedAsync(userId, postId/commentId)` to show a "Reported" state
- Validation inside the service:
  - Content exists and is not already deleted.
  - Reporter must be able to view the content (respect post visibility and friends-only rules). Never leak existence of content they cannot see.
  - A user cannot report their own content.
  - Duplicate report by the same user returns a friendly error, not an exception.
  - Description max length 2000. Reason must be a valid feed reason.
  - Simple rate limit: at most 5 reports per user per hour, to prevent report flooding. Make the limit a setting in `appsettings.json` (Options pattern, default 5) and document it in `docs/CONFIGURATION.md`.
  - When a report is dismissed, resolved or marked action-taken, notify the reporter (see section 10, Q2).
- Audit: log report submission with a new `ActionCategory` (`UserReportContent`) and details class, and log reviewer decisions. Follow `docs/AUDIT_LOGGING_SYSTEM.md` "Adding New Audit Categories". Moderator removals of content use the existing 26 and 27 categories, which should finally be written (coordinate with `system-moderator-dashboard.md`).
- Register in DI. DTOs in `DTOs/`. UI must not bind to entities.

## 5. UI

- New `Components/SocialFeed/ReportFeedContentModal.razor`, modeled on `Components/Groups/ReportContentModal.razor` (reason picker, optional description, confirm). Remove the group-rule step. Parameters: content type, content id, `OnClose`, `OnReportSubmitted`.
- Wire it in:
  - `UnifiedArticle.razor.cs`: replace the `alert("... Coming Soon")` with opening the modal for feed posts (keep the existing group flow untouched).
  - `PostPageBase.HandleReportComment` and the `ReportCommentAsync` overrides in `Home.razor`, `FriendDetails.razor`, `SinglePost.razor` and any other page that overrides it.
- Do not show "Report" on the user's own content.
- After a successful report, show the existing success alert and display the item as "Reported" for that user (no second report).
- Remove the TODO and placeholder comments that this spec resolves.

## 6. Permissions and Privacy

- Any authenticated user may report content they can view, except their own.
- Only system moderators and system administrators can list, view or act on feed reports. Enforce in the service.
- Reporter identity is visible to moderators only, never to the content author.
- Reports against friends-only content are visible to system moderators only through the report, with the minimum content needed for review.

## 7. Localization

All new strings (modal title, reasons, buttons, success and error messages, "Reported") in a new resource set `Resources/SocialFeed/ReportFeedContentModal` with neutral plus all 12 locales (ar, de, es, fr, it, ja, ko, nl, pl, pt, ru, zh) and the Designer file. Reuse existing reason strings where possible. Replace the hard-coded English alert strings with localized ones.

## 8. Tests

Unit (`FreeSpeakWeb.Tests`, xUnit, Moq, FluentAssertions, AAA):
- Service: reports a post and a comment; rejects own content, missing or deleted content, content the reporter cannot see, duplicate reports, invalid reason, over-length description; rate limit; unauthorized users cannot list or act; status transitions set reviewer and `ReviewedAt`; timestamps are set by the server.
- Rate limit: the 6th report within an hour is rejected. Reporter notification is created on each review outcome and does not expose moderator identity.
- Deletion of a post or comment handles its reports.
- bUnit: modal flow, the Report menu item hidden for own content, "Reported" state after submit.
- Integration (Testcontainers): unique index, check constraint, and the queue query.

## 9. Acceptance Criteria

1. A user can report a feed post and a feed comment from every page that shows them.
2. No "Coming Soon" placeholder remains for feed or group reporting.
3. Reports are stored with reporter, reason, description and a server-set UTC timestamp.
4. Users cannot report their own content or the same content twice, and cannot report content they cannot see.
5. Only system moderators and admins can read or act on reports (enforced in the service).
6. Deleting reported content leaves no orphaned or broken reports.
7. Submissions and reviewer decisions are audited.
8. The reporter receives a neutral in-app notification when their report is reviewed. No moderator-facing notification is sent on a new report (count only on the dashboard).
9. Reported content is never auto-hidden. A user cannot exceed 5 reports per hour.
10. All strings are localized in all locales. `dotnet build` and `dotnet test` pass. XML docs and docs updated.

## 10. Decisions (owner, resolved)

- **Q1 Moderator alerts:** count only on the dashboard for now. No notification when a report arrives.
- **Q2 Reporter outcome:** yes, the reporter is told the outcome of their report. When a moderator dismisses, resolves or takes action, send the reporter an in-app notification through the existing `NotificationService` (see `docs/NOTIFICATIONS.md`). Use neutral wording ("Your report was reviewed"). Do not reveal moderator identity or details of any action against the author. Localize it in all locales.
- **Q3 Auto-hide:** no. Reported content stays visible until a moderator acts.
- **Q4 Reasons:** reuse the existing `ReportReason` list minus group-specific reasons (`ViolatesGroupRule`, `OffTopic`).
- **Q5 Rate limit:** maximum 5 reports per user per hour (setting, default 5).

## 11. Suggested Task Breakdown

1. Entity, constraints, indexes, migration, repository, service, DTOs, DI, with tests.
2. Audit categories and details classes, with tests.
3. `ReportFeedContentModal`, wiring on all pages, localization in all locales.
4. Post and comment deletion cleanup of reports, with tests.
5. UI tests, docs, final build and test run. Review UI is delivered by `system-moderator-dashboard.md`.
