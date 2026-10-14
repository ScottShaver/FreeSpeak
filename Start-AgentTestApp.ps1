<#
.SYNOPSIS
    Starts FreeSpeak against the FreeSpeak_AgentTest database for agentic testing.
    The app listens on the Kestrel endpoints from appsettings.json: https://localhost:7025 and http://localhost:5000.
#>
[CmdletBinding()]
param(
    [string]$DatabaseName = 'FreeSpeak_AgentTest',
    [string]$DbHost = 'localhost',
    [int]$DbPort = 5432,
    [string]$DbUser = 'postgres'
)

$ErrorActionPreference = 'Stop'
if (-not $DatabaseName.EndsWith('_AgentTest', [StringComparison]::Ordinal)) {
    throw "Refusing to start: database name must end with '_AgentTest'."
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = "Host=$DbHost;Port=$DbPort;Database=$DatabaseName;Username=$DbUser"
$env:AgentTest__Enabled = 'true'
$env:Caching__UseRedis = 'false'
$env:RateLimiting__UseDistributed = 'false'

dotnet run --project (Join-Path $PSScriptRoot 'FreeSpeakWeb') --no-launch-profile
