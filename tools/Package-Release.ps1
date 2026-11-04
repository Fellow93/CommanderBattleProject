<#
.SYNOPSIS
    Zips the installed CommanderBattle and RBM.Multiplayer modules into a release for players.

.DESCRIPTION
    Takes the modules the build installed into <game>\Modules and writes dist\CommanderBattle-<version>.zip, with the
    two module folders at its root: players unzip it into <game>\Modules. The version comes from Module\SubModule.xml.

    server_address_overrides.txt comes from the repo's empty template, not the installed copy with the host's rewrites.

    SceneObj is left out: it holds junctions to the player's own SandBoxCore scenes, which the client recreates when the
    game loads (CampaignSceneLinker). Zipping it would pack gigabytes of TaleWorlds scenes.

    Players and the server must run the same build: deploy the server from the same build as the zip.

.PARAMETER GameDir
    Bannerlord install folder. Defaults to the one this repo sits in (<game>\Modules\CommanderBattleProject).

.PARAMETER Build
    Build and install the solution first (needs the game closed, for the client DLLs).

.EXAMPLE
    .\tools\Package-Release.ps1 -Build
#>
param(
    [string]$GameDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'

$repoDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modules = @('CommanderBattle', 'RBM.Multiplayer')

if ($Build) {
    Write-Host 'Building and installing the solution...'
    & dotnet build (Join-Path $repoDir 'CommanderBattle.sln') -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed (is the game still open? It locks the client DLLs).' }
}

foreach ($module in $modules) {
    if (-not (Test-Path (Join-Path $GameDir "Modules\$module\bin\Win64_Shipping_Client\$module.dll"))) { throw "Module $module is not built into $GameDir\Modules (build the solution first, or use -Build)." }
}

$version = ([xml](Get-Content (Join-Path $repoDir 'Module\SubModule.xml'))).Module.Version.value
$distDir = Join-Path $repoDir 'dist'
$zip = Join-Path $distDir "CommanderBattle-$version.zip"
$staging = Join-Path $distDir 'staging'

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force $staging | Out-Null
foreach ($module in $modules) {
    # /XJ skips junctions, so the copy never walks into the linked scenes.
    & robocopy (Join-Path $GameDir "Modules\$module") (Join-Path $staging $module) /E /XJ /XD SceneObj /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $module failed (robocopy exit code $LASTEXITCODE)." }
}
# The installed overrides hold the host's own rewrites (public IP -> 127.0.0.1): on a player's PC they would send the
# join to the player's own machine, and they publish the host's IP. Ship the repo's empty template instead.
Copy-Item (Join-Path $repoDir 'Module\ModuleData\server_address_overrides.txt') (Join-Path $staging 'CommanderBattle\ModuleData\') -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zip) { Remove-Item $zip -Force }
# Entry by entry: .NET Framework's CreateFromDirectory writes '\' separators, which the zip format doesn't allow.
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in Get-ChildItem $staging -Recurse -File) {
        $name = $file.FullName.Substring($staging.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name) | Out-Null
    }
} finally { $archive.Dispose() }
Remove-Item $staging -Recurse -Force

# Check the zip has what a player needs, and none of the scenes.
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = $archive.Entries | ForEach-Object { $_.FullName }
    if ($entries -match '\\') { throw 'The zip has entries with backslash separators.' }
    foreach ($module in $modules) {
        foreach ($required in "$module/SubModule.xml", "$module/bin/Win64_Shipping_Client/$module.dll") {
            if ($entries -notcontains $required) { throw "The zip lacks $required." }
        }
    }
    if ($entries -match '/SceneObj/') { throw 'The zip contains SceneObj.' }
} finally { $archive.Dispose() }

Write-Host "Release: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB, $($entries.Count) files)."
Write-Host 'Attach it to a GitHub release; players unzip it into <game>\Modules and enable both modules in the launcher''s Multiplayer tab.'
