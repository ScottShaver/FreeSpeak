<#
.SYNOPSIS
    Resets the FreeSpeak_AgentTest database used for agentic (AI-driven) testing.

.DESCRIPTION
    1. Drops and recreates the FreeSpeak_AgentTest database (as the postgres user; password comes from pgpass.conf).
    2. Generates an idempotent EF Core migration script (FreeSpeakWeb project) into a temp file and applies it with psql.
    3. Starts the app in Development with AgentTest:Enabled=true so roles, the system admin and the agent personas are seeded.
    4. Optionally flushes a Redis DB (-FlushRedis) and/or snapshots the seeded DB (-Snapshot) via CREATE DATABASE ... TEMPLATE.
    5. Use -RestoreSnapshot to quickly reset from a previous snapshot instead of rebuilding.

    The target database name must end with '_AgentTest'. The script refuses anything else.
#>
[CmdletBinding()]
param(
    [string]$DatabaseName = 'FreeSpeak_AgentTest',
    [string]$DbHost = 'localhost',
    [int]$DbPort = 5432,
    [string]$DbUser = 'postgres',
    [string]$PsqlPath,
    [int]$AppPort = 7025,
    [int]$StartupTimeoutSeconds = 180,
    [switch]$Snapshot,
    [switch]$RestoreSnapshot,
    [switch]$FlushRedis,
    [string]$RedisHost = 'localhost',
    [int]$RedisPort = 6379,
    [int]$RedisDb = 15
)

$ErrorActionPreference = 'Stop'
$env:PGOPTIONS = '-c client_min_messages=warning'

if (-not $DatabaseName.EndsWith('_AgentTest', [StringComparison]::Ordinal) -or $DatabaseName -notmatch '^[A-Za-z0-9_]+$') {
    throw "Refusing to reset '$DatabaseName': name must be alphanumeric/underscore and end with '_AgentTest'."
}
$snapshotName = "${DatabaseName}_snapshot"

$repoRoot = $PSScriptRoot
$webProject = Join-Path $repoRoot 'FreeSpeakWeb'

if (-not $PsqlPath) {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { $PsqlPath = $cmd.Source }
    else {
        $PsqlPath = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe', 'D:\devtools\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $PsqlPath -or -not (Test-Path $PsqlPath)) { throw 'psql not found. Pass -PsqlPath.' }

function Invoke-Psql {
    param([string]$Database = 'postgres', [string[]]$PsqlArgs)
    & $PsqlPath -h $DbHost -p $DbPort -U $DbUser -w -d $Database -v ON_ERROR_STOP=1 @PsqlArgs
    if ($LASTEXITCODE -ne 0) { throw "psql failed (exit $LASTEXITCODE)." }
}

function Invoke-PsqlSql {
    # SQL goes via stdin so embedded double quotes (case-sensitive identifiers) survive native-command argument passing.
    param([string]$Database = 'postgres', [string]$Sql)
    $Sql | & $PsqlPath -h $DbHost -p $DbPort -U $DbUser -w -d $Database -v ON_ERROR_STOP=1 -q -f -
    if ($LASTEXITCODE -ne 0) { throw "psql failed (exit $LASTEXITCODE)." }
}

function Reset-Database {
    param([string]$Name, [string]$Template)
    Invoke-PsqlSql -Sql "DROP DATABASE IF EXISTS `"$Name`" WITH (FORCE);"
    if ($Template) {
        Invoke-PsqlSql -Sql "CREATE DATABASE `"$Name`" TEMPLATE `"$Template`";"
    } else {
        Invoke-PsqlSql -Sql "CREATE DATABASE `"$Name`";"
    }
}

Write-Host "Checking PostgreSQL connectivity..."
Invoke-Psql -PsqlArgs @('-tAc', 'select 1') | Out-Null

if ($FlushRedis) {
    $redisCli = Get-Command redis-cli -ErrorAction SilentlyContinue
    if (-not $redisCli) { throw 'redis-cli not found; cannot flush Redis. Omit -FlushRedis (Redis is off by default).' }
    Write-Host "Flushing Redis DB $RedisDb on ${RedisHost}:$RedisPort..."
    & $redisCli.Source -h $RedisHost -p $RedisPort -n $RedisDb FLUSHDB
    if ($LASTEXITCODE -ne 0) { throw 'redis-cli FLUSHDB failed.' }
}

if ($RestoreSnapshot) {
    Write-Host "Restoring $DatabaseName from $snapshotName..."
    Reset-Database -Name $DatabaseName -Template $snapshotName
    Write-Host 'Restored from snapshot.'
    return
}

Write-Host "Recreating database $DatabaseName..."
Reset-Database -Name $DatabaseName

# Generate the idempotent migration script to a temp path (never committed) and apply it.
$migrationsSql = Join-Path ([IO.Path]::GetTempPath()) "$DatabaseName.migrations.sql"
Write-Host 'Generating migration script...'
Push-Location $webProject
try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    dotnet ef migrations script --idempotent --output $migrationsSql
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations script failed.' }
} finally { Pop-Location }

Write-Host 'Applying migration script...'
# dotnet ef writes a UTF-8 BOM that psql rejects; rewrite as BOM-less UTF-8.
[IO.File]::WriteAllText($migrationsSql, [IO.File]::ReadAllText($migrationsSql), (New-Object Text.UTF8Encoding($false)))
Invoke-Psql -Database $DatabaseName -PsqlArgs @('-q', '-f', $migrationsSql)
Remove-Item $migrationsSql -ErrorAction SilentlyContinue

# Start the app so seeding (roles, system admin, personas) runs. Credentials come from pgpass via the
# password-less connection string; no password is placed in this script or on the command line.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = "Host=$DbHost;Port=$DbPort;Database=$DatabaseName;Username=$DbUser"
$env:AgentTest__Enabled = 'true'
$env:Caching__UseRedis = 'false'
$env:RateLimiting__UseDistributed = 'false'

Write-Host 'Starting app to run seeding...'
$logFile = Join-Path ([IO.Path]::GetTempPath()) "$DatabaseName.app.log"
$errFile = "$logFile.err"
$proc = Start-Process dotnet -ArgumentList @('run', '--project', $webProject, '--no-launch-profile') `
    -PassThru -NoNewWindow -RedirectStandardOutput $logFile -RedirectStandardError $errFile
try {
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $seeded = $false
    while ((Get-Date) -lt $deadline) {
        if ($proc.HasExited) { throw "App exited early (code $($proc.ExitCode)). See $logFile / $errFile." }
        $count = "select count(*) from `"AspNetUsers`" where `"UserName`" like 'agent.%'" |
            & $PsqlPath -h $DbHost -p $DbPort -U $DbUser -w -d $DatabaseName -tA -f - 2>$null
        if ($LASTEXITCODE -eq 0 -and [int]$count -ge 8) { $seeded = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $seeded) { throw "Seeding did not complete within $StartupTimeoutSeconds s. See $logFile / $errFile." }
    Write-Host 'Seeding complete.'
} finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    # dotnet run spawns a child; stop any process still listening on the app port.
    Get-NetTCPConnection -LocalPort $AppPort -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

if ($Snapshot) {
    Write-Host "Creating snapshot $snapshotName..."
    Invoke-PsqlSql -Sql "DROP DATABASE IF EXISTS `"$snapshotName`" WITH (FORCE);"
    Invoke-PsqlSql -Sql "CREATE DATABASE `"$snapshotName`" TEMPLATE `"$DatabaseName`";"
}

Write-Host "Done. Start the app against $DatabaseName with: .\Start-AgentTestApp.ps1"
