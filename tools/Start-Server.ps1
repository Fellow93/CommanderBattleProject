<#
.SYNOPSIS
    Starts the local dedicated server with Commander Battle (and RBM Multiplayer), and reports when it hosts.

.DESCRIPTION
    Runs DedicatedCustomServer.Starter.exe from the dedicated server's bin folder with the modules, the server
    config written by Install-CampaignScenes.ps1, the tick rate and the port. Arguments are passed straight to the
    process, so the /options arrive intact (Git Bash rewrites them into paths: see CLAUDE.md).

    The script refuses to start while a server is already running, unless -Restart is given. -Build rebuilds and
    installs the solution first, which needs the server stopped (and the game closed, for the client DLLs).

    After starting, it waits for the server to pick a map and prints the process id, the map, the RBM patch line
    and where the logs are.

.PARAMETER ServerDir
    Dedicated server install folder. Defaults to "Mount & Blade II Dedicated Server" next to the game.

.PARAMETER Config
    Server config file in <ServerDir>\Modules\Native. Defaults to the one Install-CampaignScenes.ps1 writes.

.PARAMETER Port
    UDP port. Default 7210.

.PARAMETER TickRate
    Server tick rate. Default 60.

.PARAMETER Token
    Custom server auth token, needed to appear in the public custom server list (run customserver.gettoken in the
    game's console). Defaults to the CB_SERVER_TOKEN environment variable; without one the server only takes
    direct connections.

.PARAMETER NoRbm
    Start without RBM Multiplayer (Commander Battle with its own AI only).

.PARAMETER Restart
    Stop a running dedicated server first.

.PARAMETER Build
    Build and install the solution before starting (stops a running server, as -Restart).

.PARAMETER DryRun
    Print what would run, without stopping, building or starting anything.

.EXAMPLE
    .\tools\Start-Server.ps1 -Restart

.EXAMPLE
    .\tools\Start-Server.ps1 -Build
#>
param(
    [string]$ServerDir = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path -Parent) 'Mount & Blade II Dedicated Server'),
    [string]$Config = 'ds_config_commander_campaign.txt',
    [int]$Port = 7210,
    [int]$TickRate = 60,
    [string]$Token = $env:CB_SERVER_TOKEN,
    [switch]$NoRbm,
    [switch]$Restart,
    [switch]$Build,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binDir = Join-Path $ServerDir 'bin\Win64_Shipping_Server'
$starter = Join-Path $binDir 'DedicatedCustomServer.Starter.exe'
$logDir = 'C:\ProgramData\Mount and Blade II Bannerlord\logs'
$modules = @('Native', 'Multiplayer', 'CommanderBattle')
if (-not $NoRbm) { $modules += 'RBM.Multiplayer' }

if (-not (Test-Path $starter)) { throw "Dedicated server not found: $starter (install 'Mount & Blade II: Dedicated Server', or pass -ServerDir)." }
if (-not (Test-Path (Join-Path $ServerDir "Modules\Native\$Config"))) { throw "Server config not found: $ServerDir\Modules\Native\$Config (run tools\Install-CampaignScenes.ps1, or pass -Config)." }
foreach ($module in $modules | Where-Object { $_ -in 'CommanderBattle', 'RBM.Multiplayer' }) {
    if (-not (Test-Path (Join-Path $ServerDir "Modules\$module\SubModule.xml"))) { throw "Module $module is not installed on the server (build the solution first, or use -Build)." }
}

$arguments = @("_MODULES_*$($modules -join '*')*_MODULES_", '/dedicatedcustomserverconfigfile', $Config, '/tickrate', $TickRate, '/port', $Port)
if ($Token) { $arguments += @('/dedicatedcustomserverauthtoken', $Token) }
# The token is a secret: never print it.
$shown = ($arguments | ForEach-Object { if ($Token -and $_ -eq $Token) { '<token>' } else { $_ } }) -join ' '

$running = @(Get-Process -Name 'DedicatedCustomServer*' -ErrorAction SilentlyContinue)
if ($DryRun) {
    if ($running.Count) { Write-Host "Running server: pid $($running.Id -join ', ')$(if ($Restart -or $Build) { ' (would be stopped)' } else { ' (start would be refused; use -Restart)' })" }
    if ($Build) { Write-Host "Would build: dotnet build `"$repoDir\CommanderBattle.sln`" -c Release" }
    Write-Host "Would run in ${binDir}:"
    Write-Host "  DedicatedCustomServer.Starter.exe $shown"
    return
}

if ($running.Count) {
    if (-not ($Restart -or $Build)) { throw "A dedicated server is already running (pid $($running.Id -join ', ')). Use -Restart to stop it first." }
    Write-Host "Stopping the running server (pid $($running.Id -join ', '))..."
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
}

if ($Build) {
    Write-Host 'Building and installing the solution...'
    & dotnet build (Join-Path $repoDir 'CommanderBattle.sln') -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed (is the game still open? It locks the client DLLs).' }
}

Write-Host "Starting: DedicatedCustomServer.Starter.exe $shown"
$started = Get-Date
Start-Process -FilePath $starter -ArgumentList $arguments -WorkingDirectory $binDir | Out-Null

# The starter runs the server itself; find it by its start time.
$server = $null
for ($i = 0; $i -lt 30 -and -not $server; $i++) {
    Start-Sleep -Seconds 1
    $server = Get-Process -Name 'DedicatedCustomServer*' -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $started.AddSeconds(-2) } | Select-Object -First 1
}
if (-not $server) { throw 'The server did not start: check the newest rgl_log_*.txt and watchdog_log_*.txt in ' + $logDir }
Write-Host "Server running, pid $($server.Id)."

# Wait for the map, then show the map and the RBM patch line.
$rgl = Join-Path $logDir "rgl_log_$($server.Id).txt"
$hosting = $null
for ($i = 0; $i -lt 90 -and -not $hosting -and -not $server.HasExited; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path $rgl) { $hosting = Select-String -Path $rgl -Pattern '--Selected scene: (\S+)' | Select-Object -First 1 }
}
if ($server.HasExited) { throw "The server exited (code $($server.ExitCode)). See $rgl." }
if ($hosting) { Write-Host "Hosting Commander Battle on $($hosting.Matches[0].Groups[1].Value)." } else { Write-Warning "No map selected yet after 90 s; see $rgl." }
$rbmLog = Join-Path $logDir "RBM.Multiplayer_$($server.Id).log"
if (-not $NoRbm -and (Test-Path $rbmLog)) { Select-String -Path $rbmLog -Pattern 'Patches:' | Select-Object -First 1 | ForEach-Object { Write-Host "RBM: $($_.Line.Substring($_.Line.IndexOf('Patches:')))" } }
Write-Host "Logs: $logDir\CommanderBattle_$($server.Id).log, RBM.Multiplayer_$($server.Id).log, rgl_log_$($server.Id).txt"
