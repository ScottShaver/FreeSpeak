# Copilot Instructions

## General Guidelines
- Whenever you create or update a class method, make sure the C# documentation comments are added and correct.
- Whenever you have to make a plan for a prompt, break it into many steps and stop after each step asking me if I want you to continue, so we can avoid token and rate limit issues.
- Whenever you add a NuGet package to the solution, make sure to update the Technology Credits page.
- Do not use PowerShell helper scripts or multi-line terminal commands to create files. Use the create_file tool directly instead.

## Local database and agent testing
- A local PostgreSQL server runs on localhost:5432. The disposable test database is `FreeSpeak_AgentTest`; see `docs/AGENT_TESTING.md` for the reset and start scripts, test personas and charters.
- Only run `Reset-AgentTestDb.ps1` and `Start-AgentTestApp.ps1` for testing. They touch only `FreeSpeak_AgentTest`.
- Never run migrations, resets, seeders or destructive SQL against any other database, and never change connection strings to point at one.
- Database credentials live outside the repo (the user's `pgpass.conf`). Never read, print, log or commit them.
- Run the scripts with `powershell -ExecutionPolicy Bypass -File ...`. The seeder only runs in Development with `AgentTest:Enabled` and a database name ending `_AgentTest`; do not weaken these guards.
