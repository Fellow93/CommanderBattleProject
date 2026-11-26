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

.PARAMETER ServerName
    The name in the custom server list. Without it, the name already in the config is kept ("Commander Battle"
    in a new config). Pass an empty string to go back to that default.

.PARAMETER GamePassword
    Written to the server config as GamePassword: players need it to join. Without it, the password already in
    the config is kept. Pass an empty string to remove it.

.PARAMETER AdminPassword
    Written to the server config as AdminPassword. Players who join with it are admins and get the admin
    panel. Without it, the password already in the config is kept. Pass an empty string to remove it.

.PARAMETER CultureTeam1
    The first side's culture (default vlandia). This and the match settings below are kept on a rerun, as the
    name is.

.PARAMETER CultureTeam2
    The second side's culture (default battania).

.PARAMETER RoundTimeLimit
    Length of a round in seconds, 60 to 3600 (default 900). The limits are the game's.

.PARAMETER RoundPreparationTimeLimit
    Preparation time at the start of a round in seconds, 2 to 60 (default 45).

.PARAMETER RoundTotal
    Rounds per map, 1 to 99 (default 2).

.PARAMETER AllowPollsToChangeMaps
    True or False (default True): players can call a vote to change the map.

.PARAMETER MapVoting
    True or False (default True): players vote for the next map after each match.
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ServerDir = (Join-Path (Split-Path $GameDir -Parent) 'Mount & Blade II Dedicated Server'),
    [switch]$Copy,
    [string]$ServerName,
    [string]$GamePassword,
    [string]$AdminPassword,
    [ValidateSet('vlandia', 'empire', 'khuzait', 'aserai', 'sturgia', 'battania')][string]$CultureTeam1,
    [ValidateSet('vlandia', 'empire', 'khuzait', 'aserai', 'sturgia', 'battania')][string]$CultureTeam2,
    [ValidateRange(60, 3600)][int]$RoundTimeLimit,
    [ValidateRange(2, 60)][int]$RoundPreparationTimeLimit,
    [ValidateRange(1, 99)][int]$RoundTotal,
    [ValidateSet('True', 'False')][string]$AllowPollsToChangeMaps,
    [ValidateSet('True', 'False')][string]$MapVoting
)

$ErrorActionPreference = 'Stop'

# The config takes the rest of the line as the value, so a line break would start a new command.
if ("$ServerName$GamePassword$AdminPassword" -match '[\r\n]') { throw 'ServerName, GamePassword and AdminPassword must each be on one line.' }

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
    # The config is rewritten from scratch, so the name, the passwords and the match settings of an earlier run are
    # carried over unless one is given. A given empty value removes the option, or brings back its default.
    $given = $PSBoundParameters
    $old = @(if (Test-Path $config) { Get-Content $config })
    function Get-OptionLine([string]$option, [string]$default) {
        if ($given.ContainsKey($option)) { $value = $given[$option] }
        else { $value = ($old | Where-Object { $_ -match "^$option\s+\S" } | Select-Object -First 1) -replace "^$option\s+", '' }
        if (-not $value) { $value = $default }
        if ($value) { "$option $value" }
    }
    $passwordLine = Get-OptionLine AdminPassword
    # Map voting is a command, not an option: on in a new config, and otherwise as the old config had it.
    # (Not named $mapVoting: that is the string parameter, and "False" as a string counts as true.)
    $voteForMaps = if ($given.ContainsKey('MapVoting')) { $MapVoting -eq 'True' } else { (-not $old.Count) -or [bool]($old -match '^enable_map_voting\s*$') }
    $lines = @(
        Get-OptionLine ServerName 'Commander Battle'
        'GameType CommanderBattle'
        Get-OptionLine CultureTeam1 'vlandia'
        Get-OptionLine CultureTeam2 'battania'
        'MaxNumberOfPlayers 18'
        Get-OptionLine RoundPreparationTimeLimit '45'
        Get-OptionLine RoundTimeLimit '900'
        Get-OptionLine RoundTotal '2'
        'MapTimeLimit 60'
        Get-OptionLine AllowPollsToChangeMaps 'True'
    )
    # Before start_game: the passwords reach the lobby when the server registers.
    $lines += @(Get-OptionLine GamePassword)
    if ($passwordLine) { $lines += $passwordLine }
    # The intermission vote offers every map of the pool (the game caps it at 100).
    $lines += $scenes | ForEach-Object { "add_map_to_automated_battle_pool $_" }
    # The admin panel's map list (Mission Options) shows the usable maps, a separate list from the vote pool.
    $lines += $scenes | ForEach-Object { "add_map_to_usable_maps $_ CommanderBattle" }
    $lines += @(
        'set_automated_battle_count -1'
        if ($voteForMaps) { 'enable_map_voting' }
        'start_game'
        'enable_automated_battle_switching'
    )
    Set-Content -Path $config -Value $lines -Encoding UTF8
    Write-Host "Server config written: $config ($(if ($passwordLine) { 'with' } else { 'no' }) admin password)"
}
