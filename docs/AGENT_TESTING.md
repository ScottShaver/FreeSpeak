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
| `agent.owner` | Group owner | Creator and admin of both groups. |
| `agent.moderator` | Group moderator | Moderator in both groups. |
| `agent.member` | Group member | Member of the open group only. Author of 3 feed posts (public, friends-only, only-me). |
| `agent.banned` | Banned user | Banned from "Agent Test Open Group". |
| `agent.newuser` | New user | No friends, groups, or posts. |
| `agent.friend` | Friend | Accepted friend of `agent.member` and `agent.owner`. |
| `agent.stranger` | Stranger | No friendships or groups. |

Seeded data: 2 groups ("Agent Test Open Group": public; "Agent Test Private Group": private, join and post approval required), 3 group posts, 3 feed posts, 3 comments. Timestamps are fixed, so data is identical on every reset.

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

> Only use `https://localhost:7025`. Log in only with the persona given. Do not visit any other site. Do not attempt to change the database or files directly. Stop and report if the site is not the FreeSpeak test instance (the title/site name is "FreeSpeakDemo").

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

Status after the first QA round (pilot as member, stranger, banned, moderator, owner, friend, sysadmin). The high and medium findings were fixed in PR #55.

### Retest (narrow charters, fixed items only)
- Banned user cannot read `/group/N/post/M`; public group `/group/1` is viewable by a non-member and shows Join; private group stays hidden.
- Logout from any page, including with a modal open, returns to the home page.
- A system admin cannot remove the last SystemAdministrator role; self role edit asks for confirmation; role changes appear in the audit log.
- Moderator and owner see Delete on other members' group posts.
- Confirmations appear for Ban Member, Unfriend and Delete comment; double-clicking Post creates one post; a 10k-character post shows "See more".

### Not yet tested
- Group join-request and post approval flows. The seed has no pending data, and the private group is not discoverable, so add pending requests and posts to `AgentTestSeeder`.
- A creator leaving or being removed from a group (no Leave button for the creator today).
- Demoting or deleting the last admin; suspend and ban from the admin UI (menu items are disabled).
- Only-me post count leak on profiles (shows 6 posts to a friend, 5 visible).
- Groups: stale member count on the Find Groups card after a ban.
- Other feature areas: friend request acceptance, notifications (seed data is dated Jan 2026, so notifications showed 0), file uploads, group files, search, settings and the language switcher on admin pages.
- Reply depth: UI showed 5 tiers; the documented maximum is 4.
- `agent.newuser` persona and the cross-cutting probes in section 7.

### Known open items (not from QA)
- Dependency vulnerabilities: SixLabors.ImageSharp 3.1.12 (3 high, 2 moderate) and AngleSharp 0.17.1 (1 moderate). ImageSharp processes user uploads, so fix before launch.
- Low-severity UI gaps: Mod Log tab is a stub, moderators have no member list or ban UI, Escape does not close the post popup menu, `/system-moderator` is a placeholder.
- 43 unit tests fail (UnifiedArticle, PostEditModal, GroupFileService; missing DI/setup). Confirm they also fail on `master` and fix.
- Dev-only 404 for `hot-reload-settings.json` on every page load.

### Tips for the next run
- Run `Reset-AgentTestDb.ps1` before each session. One browser session per persona, with a fresh browser context when switching users.
- Keep charters short (about 35 actions per role) to control cost; browser testing is the most expensive part.
