<#
.SYNOPSIS
    Makes the campaign field-battle scenes available to Commander Battle and writes a server config where
    players vote for the next one after each match.

.DESCRIPTION
    Multiplayer only loads scenes from multiplayer modules, so the scenes the campaign uses for field battles
    (listed in Modules\SandBox\ModuleData\sp_battle_scenes.xml) are linked into Modules\CommanderBattle\SceneObj
    of the game and of the dedicated server. Directory junctions are used by default (no extra disk space,
    no admin rights); -Copy copies the folders instead, e.g. for a server on another machine or drive.

    The scenes come from your own game install and are not shipped with the mod.

.PARAMETER GameDir
    Bannerlord install folder. Defaults to two folders above this repo (<game>\Modules\CommanderBattleProject).

.PARAMETER ServerDir
    Dedicated server install folder. Defaults to "Mount & Blade II Dedicated Server" next to the game.
    Skipped if it does not exist.

.PARAMETER Copy
    Copy the scene folders instead of creating junctions.

.PARAMETER AdminPassword
    Written to the server config as AdminPassword. Players who join with it are admins and get the admin
    panel. Without it, the password already in the config is kept. Pass an empty string to remove it.
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ServerDir = (Join-Path (Split-Path $GameDir -Parent) 'Mount & Blade II Dedicated Server'),
    [switch]$Copy,
    [string]$AdminPassword
)

$ErrorActionPreference = 'Stop'

# The config takes the rest of the line as the value, so a line break would start a new command.
if ($AdminPassword -match '[\r\n]') { throw 'AdminPassword must be on one line.' }

$sceneList = Join-Path $GameDir 'Modules\SandBox\ModuleData\sp_battle_scenes.xml'
$sceneSource = Join-Path $GameDir 'Modules\SandBoxCore\SceneObj'
if (-not (Test-Path $sceneList)) { throw "Scene list not found: $sceneList" }

[xml]$xml = Get-Content $sceneList -Raw
$scenes = @($xml.SPBattleScenes.Scene | ForEach-Object { $_.id } | Sort-Object -Unique | Where-Object { Test-Path (Join-Path $sceneSource $_) })
Write-Host "$($scenes.Count) campaign field-battle scenes found."

$targets = @(Join-Path $GameDir 'Modules\CommanderBattle')
if (Test-Path $ServerDir) { $targets += Join-Path $ServerDir 'Modules\CommanderBattle' }

foreach ($module in $targets) {
    if (-not (Test-Path $module)) {
        Write-Warning "Skipping $module (build the mod first)."
        continue
    }
    $sceneObj = Join-Path $module 'SceneObj'
    New-Item -ItemType Directory -Force $sceneObj | Out-Null
    $added = 0
    foreach ($scene in $scenes) {
        $target = Join-Path $sceneObj $scene
        if (Test-Path $target) { continue }
        $source = Join-Path $sceneSource $scene
        if ($Copy) {
            Copy-Item $source $target -Recurse
        }
        else {
            New-Item -ItemType Junction -Path $target -Target $source | Out-Null
        }
        $added++
    }
    Write-Host "$module : $added scenes added ($($scenes.Count - $added) already there)."
}

# Server config: the players vote for the next scene from the pool after every match, forever.
if (Test-Path $ServerDir) {
    $config = Join-Path $ServerDir 'Modules\Native\ds_config_commander_campaign.txt'
    # The config is rewritten from scratch, so a password set on an earlier run is carried over unless one is given.
    $passwordLine = $null
    if ($PSBoundParameters.ContainsKey('AdminPassword')) {
        if ($AdminPassword) { $passwordLine = "AdminPassword $AdminPassword" }
    }
    elseif (Test-Path $config) {
        $passwordLine = Get-Content $config | Where-Object { $_ -match '^AdminPassword\s+\S' } | Select-Object -First 1
    }
    $lines = @(
        'ServerName Commander Battle'
        'GameType CommanderBattle'
        'CultureTeam1 vlandia'
        'CultureTeam2 battania'
        'MaxNumberOfPlayers 16'
        'RoundPreparationTimeLimit 45'
        'RoundTimeLimit 900'
        'RoundTotal 2'
        'MapTimeLimit 60'
        'AllowPollsToChangeMaps True'
    )
    # Before start_game: the password reaches the lobby when the server registers.
    if ($passwordLine) { $lines += $passwordLine }
    # The intermission vote offers every map of the pool (the game caps it at 100).
    $lines += $scenes | ForEach-Object { "add_map_to_automated_battle_pool $_" }
    # The admin panel's map list (Mission Options) shows the usable maps, a separate list from the vote pool.
    $lines += $scenes | ForEach-Object { "add_map_to_usable_maps $_ CommanderBattle" }
    $lines += @(
        'set_automated_battle_count -1'
        'enable_map_voting'
        'start_game'
        'enable_automated_battle_switching'
    )
    Set-Content -Path $config -Value $lines -Encoding UTF8
    Write-Host "Server config written: $config ($(if ($passwordLine) { 'with' } else { 'no' }) admin password)"
}
