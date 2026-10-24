<#
.SYNOPSIS
    Makes the campaign field-battle scenes available to Commander Battle and writes a server config that
    plays a random one each match.

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
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ServerDir = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path -Parent) 'Mount & Blade II Dedicated Server'),
    [switch]$Copy
)

$ErrorActionPreference = 'Stop'

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

# Server config: a random scene from the pool for every match, forever.
if (Test-Path $ServerDir) {
    $config = Join-Path $ServerDir 'Modules\Native\ds_config_commander_campaign.txt'
    $lines = @(
        'ServerName Commander Battle'
        'GameType CommanderBattle'
        'CultureTeam1 vlandia'
        'CultureTeam2 battania'
        'MaxNumberOfPlayers 32'
        'RoundPreparationTimeLimit 45'
        'RoundTimeLimit 900'
        'RoundTotal 2'
        'MapTimeLimit 60'
        'AllowPollsToChangeMaps False'
    )
    $lines += $scenes | ForEach-Object { "add_map_to_automated_battle_pool $_" }
    $lines += @(
        'set_automated_battle_count -1'
        'disable_map_voting'
        'start_game'
        'enable_automated_battle_switching'
    )
    Set-Content -Path $config -Value $lines -Encoding UTF8
    Write-Host "Server config written: $config"
}
