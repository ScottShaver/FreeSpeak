# Agentic Testing Guide

How to let an AI agent (GitHub Copilot driving a real browser through Playwright MCP) test FreeSpeak by logging in as users with different roles.

> **Safety first.** Agents click real buttons. They only ever run against the dedicated `FreeSpeak_AgentTest` database, which is disposable. Never point an agent at your normal dev database or at production.

## 1. How it is protected

- The test data lives in its own PostgreSQL database, `FreeSpeak_AgentTest`.
- `AgentTestSeeder` (`FreeSpeakWeb/Data/AgentTestSeeder.cs`) refuses to run unless **all** are true:
  - `ASPNETCORE_ENVIRONMENT=Development`
  - `AgentTest:Enabled=true` (set only by the scripts below, as environment variable `AgentTest__Enabled`)
  - the connected database name ends with `_AgentTest`
- If any check fails, the app throws at startup instead of seeding. It cannot seed in Production.
- When `AgentTest:Enabled` is on, the older random dev seeding is skipped so the data is deterministic.
- The scripts also refuse any database name not ending in `_AgentTest`.
- Personas use fake `@agenttest.invalid` emails (the `.invalid` TLD can never receive mail), and email sending is a no-op in `appsettings.json`.

## 2. Prerequisites

- PostgreSQL running on `localhost:5432`. The scripts connect as `postgres` and rely on your `%APPDATA%\postgresql\pgpass.conf`, so no password appears in scripts or connection strings.
- .NET SDK and the EF tool (`dotnet tool install --global dotnet-ef`).
- Redis is **not required**. The scripts force `Caching__UseRedis=false`.
- Node.js (for Playwright MCP).

## 3. Reset and start

Schema creation follows the repo convention: an idempotent EF migration script is generated and applied with `psql`.

```powershell
# Drop/recreate FreeSpeak_AgentTest, apply migrations, seed roles + personas
powershell -ExecutionPolicy Bypass -File .\Reset-AgentTestDb.ps1

# Same, and also save a snapshot (FreeSpeak_AgentTest_snapshot)
powershell -ExecutionPolicy Bypass -File .\Reset-AgentTestDb.ps1 -Snapshot

# Fast reset from the snapshot (skips migrations and seeding)
powershell -ExecutionPolicy Bypass -File .\Reset-AgentTestDb.ps1 -RestoreSnapshot

# Start the app against the test database
powershell -ExecutionPolicy Bypass -File .\Start-AgentTestApp.ps1
```

What `Reset-AgentTestDb.ps1` does:

1. Verifies PostgreSQL connectivity.
2. Drops and recreates `FreeSpeak_AgentTest`.
3. Runs `dotnet ef migrations script --idempotent` in `FreeSpeakWeb` to a temp file (never committed; the repo ignores migration scripts) and applies it with `psql -f`.
4. Starts the app with `ConnectionStrings__DefaultConnection` pointing at the test DB so the normal seeding (roles, system admin) and the persona seeding run, then stops it.
5. Optional: `-Snapshot` (`CREATE DATABASE ... TEMPLATE`), `-FlushRedis` (flushes Redis DB 15 via `redis-cli`; only needed if you enable Redis yourself).

Run the reset **before every agent session** so each run starts from the same state.

App URLs: `https://localhost:7025` (and `http://localhost:5000`, which redirects to HTTPS). Trust the dev certificate once with `dotnet dev-certs https --trust`.

## 4. Personas

All personas share the dev-only password **`AgentTest123!`**. Emails end in `@agenttest.invalid`. Log in with the username.

| Username | Role / state | Notes |
|---|---|---|
| `agent.sysadmin` | System administrator | Global `SystemAdministrator` role (admin pages). |
| `agent.sysadmin2` | Second system administrator | Same role as `agent.sysadmin`; use it for last-admin / self-demotion tests so `agent.sysadmin` is untouched. |
| `agent.sysmod` | System moderator | Global `SystemModerator` role (moderator dashboard at `/system-moderator`; cannot manage users or settings). |
| `agent.owner` | Group owner | Creator and admin of all three groups. |
| `agent.moderator` | Group moderator | Moderator in groups 1 and 2; the only non-creator **admin** of group 3. |
| `agent.member` | Group member | Member of groups 1 and 3. Author of 7 feed posts (see below). |
| `agent.banned` | Banned user | Banned from "Agent Test Open Group". |
| `agent.newuser` | New user | No friends, groups, or posts. Pending join request to the private group. |
| `agent.friend` | Friend | Accepted friend of `agent.member` and `agent.owner`. Approved member of the private group with 2 pending posts. |
| `agent.stranger` | Stranger | No accepted friendships or groups. Pending join request to the private group and pending friend request to `agent.member`. |

Seeded data (timestamps are fixed from 2026-01-01 12:00 UTC, so data is identical on every reset). Original rows keep their IDs; new rows are appended.

| Area | IDs / details |
|---|---|
| Groups | 1 Open Group (public), 2 Private Group (private, join and post approval), 3 Admin Group (public; owner creator + `agent.moderator` admin + `agent.member`) |
| Pending join requests | `GroupJoinRequests` 1 (`agent.newuser`) and 2 (`agent.stranger`) to group 2 |
| Pending group posts | `GroupPosts` 4 and 5 in group 2 by `agent.friend` (status Pending; 4 = approve, 5 = decline) |
| Group posts | 1, 2 (group 1), 3 (group 2) approved |
| Friend requests | `Friendships` 1, 2 accepted; 3 pending from `agent.stranger` to `agent.member` |
| Feed posts by `agent.member` | 1 public, 2 friends-only, 3 only-me; 4 public, 5 friends-only, 6 only-me (extra); Posts 1,4,7 are public, 2,5 friends-only, 3,6 only-me. `agent.member` sees all 7, `agent.friend` 5, others 3; the profile Posts count still includes only-me posts (known issue reproducible) |
| Comments | `Comments` 1, 2 on post 1; 3 to 7 on post 7: top-level comment 3 with a chain of replies 4, 5, 6, 7 (depth 0 to 4). `MaxFeedPostCommentDepth` is 4, so 7 is at the maximum and has no Reply button |
| Group comments | `GroupPostComments` 1 on group post 2 |
| Feed content reports | `FeedContentReports` 1 to 5: report 1 (Spam, 5 days old) and 2 (Harassment, 4 days old) on public post 1 by `agent.member`; 3 (Misinformation, 10 days old) on a post by `agent.friend`; 4 (Spam, 2 h old) on a post by `agent.stranger`; 5 (HateSpeech, 1 h old) on `agent.stranger`'s comment. Ages are relative to seeding time, so with `Moderation:StaleReportDays` = 3 reports 1 to 3 are stale. |
| Group content reports | `GroupContentReports` 1 to 4 across groups 1-3: report 1 (Spam, 6 days old, stale) on a group 1 post by `agent.member`; 2 (Harassment, 4 days old, stale) on a group 3 post by `agent.owner`; 3 (Misinformation, 3 h old) on a group 2 post by `agent.friend`; 4 (HateSpeech, 1 h old) on a group 1 comment by `agent.member`. Reviewed in the system moderator dashboard Group Reports tab; actions appear in the Moderation Activity tab and in the group Mod Log with a "System" badge. |
| Notifications | `UserNotifications` 1 to 3 for `agent.member` (friend request, unread system, read system); 4 to 6 for `agent.owner` (2 unread, 1 read) |
| Group files | File uploads and file approval are enabled on group 1 (the open group). `GroupFiles` 1 (approved) and 2 (pending) in group 1, uploaded by `agent.member`. Records only; no file exists on disk, so listing and approval work but download does not (the page shows a "file no longer available" message) |

Notifications use `ExpiresAt = NULL`. Cleanup (`DeleteExpiredNotificationsAsync`, see `docs/NOTIFICATION_CLEANUP.md`) only deletes rows with `ExpiresAt < now`, so these never get purged even though `CreatedAt` is January 2026.

Not seeded: image posts (`PostImages`/`GroupPostImages`). They need real image files on disk, and a record pointing at a missing file only shows a broken image.

The system admin from `appsettings.json` (`sysadmin` / `SysAdmin123!`) also exists.

## 5. Playwright MCP setup for Copilot

Add Playwright MCP to your Copilot CLI MCP config (`~/.copilot/mcp-config.json`), or run `/mcp add` in the CLI:

```json
{
  "mcpServers": {
    "playwright": {
      "type": "local",
      "command": "npx",
      "args": ["@playwright/mcp@latest", "--isolated", "--ignore-https-errors"],
      "tools": ["*"]
    }
  }
}
```

- `--isolated` gives each session a clean browser profile, so one persona's login never leaks into another's.
- `--ignore-https-errors` avoids failures if the dev certificate is not trusted.
- First use downloads a browser; if asked, run `npx playwright install chromium`.

For VS Code, add the same server under `"servers"` in `.vscode/mcp.json`.

## 6. Running a test session

1. Run `Reset-AgentTestDb.ps1`, then `Start-AgentTestApp.ps1` (leave it running).
2. Open Copilot and give it a charter (below). One persona per browser session.
3. Ask it to report findings as: steps, expected, actual, severity. Do not let it modify code during a test run.

Standing rules to include in every charter:

> Only use `https://localhost:7025`. Log in only with the persona given. Do not visit any other site. Do not attempt to change the database or files directly. Stop and report if the site is not the FreeSpeak test instance (the title/site name is "FreeSpeakDemo"). Close every browser window and tab you opened (call the Playwright `browser_close` tool) when the test run ends, including when it ends early or fails. Do not leave browser processes running.

### Browser cleanup rule

Every agent that opens a browser through Playwright must shut it down before finishing, whether the run passed, failed or was abandoned:

- Call `browser_close` (and close any extra tabs) at the end of every test run and before switching personas.
- Do this before sending the final report, so leftover windows never outlive the session.
- If a run is interrupted, close the browser first, then report.
- Also stop the test app started with `Start-AgentTestApp.ps1` by its specific process ID once testing is done, unless the user asked to keep it running.

## 7. Example charters

Charter format: **Persona** / **Allowed** / **Forbidden (must be denied)**. A failure is any forbidden action that succeeds, or any allowed action that fails.

### agent.newuser
- Allowed: edit own profile, create a feed post, send a friend request to `agent.stranger`, browse and join the open group, search users.
- Forbidden: viewing `agent.member`'s friends-only or only-me posts, viewing the private group's content, accessing admin pages.

### agent.member
- Allowed: post and comment in the open group, like posts, edit/delete own posts and comments, leave the group.
- Forbidden: deleting someone else's post or comment, moderator actions (pin, approve, remove, ban), editing group settings, admin pages.

### agent.moderator
- Allowed: moderate content in the groups (remove/decline posts and comments, handle reports, pin posts), approve join requests.
- Forbidden: deleting the group, changing the owner, system administration pages, changing another user's password.

### agent.owner
- Allowed: edit group settings and rules, promote/demote moderators, ban/unban members, approve requests.
- Forbidden: system administration pages, acting in groups it does not belong to.

### agent.banned
- Allowed: logging in, using the feed and profile normally.
- Forbidden: viewing or joining "Agent Test Open Group", posting or commenting in it, re-joining via direct URL or API calls.

### agent.friend
- Allowed: view `agent.member`'s public **and** friends-only posts, comment on them.
- Forbidden: viewing `agent.member`'s only-me post, editing member's posts.

### agent.stranger
- Allowed: view `agent.member`'s public post, request to join the private group.
- Forbidden: viewing friends-only or only-me posts, reading private group content before approval, any moderator/admin action.

### agent.sysadmin
- Allowed: open admin pages, manage users and roles, review audit logs.
- Forbidden for this test (do not do even though allowed): deleting the seeded persona accounts, changing persona passwords, or changing site-wide settings. Reset the database instead.

### Cross-cutting probes (any persona)
- Direct URL access to another user's private resources (IDs in URLs) returns not found or access denied.
- Forms reject script/HTML in text fields (XSS) and very long input.
- Logging out ends the session; the back button does not restore access.
- Five wrong passwords trigger lockout (resets on next database reset).

## 8. Troubleshooting

- **`running scripts is disabled`**: use `powershell -ExecutionPolicy Bypass -File ...`.
- **`psql` not found**: pass `-PsqlPath "C:\Program Files\PostgreSQL\<ver>\bin\psql.exe"`.
- **Seeding refused / app throws at startup**: the database name does not end with `_AgentTest`, or the environment is not Development. This is the safety check working.
- **App log from the reset run**: `%TEMP%\FreeSpeak_AgentTest.app.log` and `.err`.
- **`dotnet ef` not found**: run `dotnet tool restore` inside `FreeSpeakWeb` (the tool manifest lives there).
- **Playwright certificate errors**: add `--ignore-https-errors` to the Playwright MCP args.
- **`npx` blocked by execution policy**: use `npx.cmd` in the MCP config.

## 9. Next steps

Status after QA rounds 1 and 2 plus two retests. Findings were fixed in PRs #55, #61, #62, #63 and #64 and verified in the browser.

### Covered and passing
- Group join-request and post approval/decline (with author notifications), ban/unban with confirmation and success messages, consistent member counts.
- Moderator and owner Delete on others' group posts; moderator denied `/system-admin`.
- Last SystemAdministrator cannot be removed; role changes (and blocked attempts) appear in the target user's audit log.
- Friend request accept, notifications mark-read, reply depth (5 tiers), double-click Post, confirmations, Escape on popup menus.
- Profile post counts for self, friend and stranger; group visibility for non-members; logout from any page including with a modal open.
- 10k-character post "See more", image upload validation (fake, oversized, non-image), search, language switcher, settings, 404 page, script/HTML rendered safely.

### Design decisions
- Unbanning a member does not restore group membership (intended).
- Reply nesting is 5 tiers (`MaxFeedPostCommentDepth` = 4).
- User search is word-based.

### Not yet tested
- Group files list and approval (records only, no downloads).
- Group 3 last-admin demotion; creator leaving a group (no Leave button today).
- Multi-image posts, My Uploads, theme and timezone settings, admin-page language switching, very long names.
- Real file downloads and seeded image posts.

### Known open items
- Localization: validation messages show English field names under non-English UI; `<html lang>` stays `en` after a language switch.
- Audit log Details column is blank until "..." is clicked, then shows raw JSON.
- Low-severity UI gaps: Mod Log tab is a stub, moderators have no member list or ban UI, `/system-moderator` is a placeholder, admin suspend/ban menu items are disabled.
- Group-post count on profiles is not viewer-aware; PostEditModal upload errors are not localized.
- About 24 unit tests fail on `master` (GroupFileService, UnifiedArticle); they need deeper setup or updated expectations.
- ImageSharp 4.x needs a Six Labors license (committed as `sixlabors.lic`); confirm the community license covers 4.x.
- Dev-only 404 for `hot-reload-settings.json` on every page load.

### Tips for the next run
- Run `Reset-AgentTestDb.ps1` before each session. One browser session per persona, with a fresh browser context when switching users.
- Keep charters short (about 35 actions per role) to control cost; browser testing is the most expensive part.
