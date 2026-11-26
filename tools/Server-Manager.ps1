<#
.SYNOPSIS
    A small window for the host: is the server running, is everything installed, and buttons to start, stop, build,
    install the maps and set the server's name, join password and admin password.

.DESCRIPTION
    It only shows state and calls the other tools: Start-Server.ps1 (start, restart, stop), dotnet build and
    Install-CampaignScenes.ps1 (maps, config, admin password). Long jobs open in their own console window, so their
    output stays readable and this window stays responsive.

    The folders are found as the other tools find them: the game two folders above the repo, the dedicated server next
    to the game.

.PARAMETER Check
    Print the server state and the setup checks as text, and exit without opening the window.

.PARAMETER Snapshot
    Self-check: draw the window into this PNG file and exit.
#>
param(
    [switch]$Check,
    [string]$Snapshot
)

$ErrorActionPreference = 'Stop'

$gameDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$serverDir = Join-Path (Split-Path $gameDir -Parent) 'Mount & Blade II Dedicated Server'
$repoDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logDir = Join-Path $env:ProgramData 'Mount and Blade II Bannerlord\logs'
$config = Join-Path $serverDir 'Modules\Native\ds_config_commander_campaign.txt'
$startScript = Join-Path $PSScriptRoot 'Start-Server.ps1'
$installScript = Join-Path $PSScriptRoot 'Install-CampaignScenes.ps1'

function Get-Server {
    @(Get-Process -Name 'TaleWorlds.Starter.DotNetCore', 'DedicatedCustomServer*' -ErrorAction SilentlyContinue) | Select-Object -First 1
}

function Get-ConfigValue([string]$option) {
    if (Test-Path $config) { (Get-Content $config | Where-Object { $_ -match "^$option\s+\S" } | Select-Object -First 1) -replace "^$option\s+", '' }
}

# The host's combat mode: a line "RBM" or "Light" in the server's combat_mode.txt, '#' for comments; no file is Light
# (CombatModeSetting).
$combatFile = Join-Path $serverDir 'Modules\CommanderBattle\ModuleData\combat_mode.txt'
function Test-RbmCombat {
    (Test-Path $combatFile) -and ((Get-Content $combatFile | Where-Object { $_.Trim() -and $_ -notmatch '^\s*#' } | Select-Object -First 1) -match '^\s*RBM\s*$')
}

function Get-StatusText {
    # From the files: a running server still has the name, passwords and combat mode it started with.
    $name = "Name: $(Get-ConfigValue ServerName), $(if (Get-ConfigValue GamePassword) { 'password needed to join' } else { 'open to everyone' }), RBM Combat $(if (Test-RbmCombat) { 'on' } else { 'off' })"
    $server = Get-Server
    if (-not $server) { return "Server: stopped`n$name" }
    $text = "Server: running (pid $($server.Id))"
    $rgl = Join-Path $logDir "rgl_log_$($server.Id).txt"
    if (Test-Path $rgl) {
        $map = Select-String -Path $rgl -Pattern '--Selected scene: (\S+)' | Select-Object -Last 1
        if ($map) { $text += ", map $($map.Matches[0].Groups[1].Value)" } else { $text += ', starting...' }
    }
    $rbmLog = Join-Path $logDir "RBM.Multiplayer_$($server.Id).log"
    if (Test-Path $rbmLog) {
        $patches = Select-String -Path $rbmLog -Pattern 'Patches:.*' | Select-Object -First 1
        if ($patches) { $text += "`nRBM $($patches.Matches[0].Value)" }
    }
    "$text`n$name"
}

# $fix is shown while the check fails, $info while it passes. An optional check is not needed to host.
function New-Check([string]$name, [bool]$ok, [string]$fix, [string]$info, [switch]$Optional) {
    [pscustomobject]@{
        Name   = $name
        State  = $(if ($ok) { 'OK' } elseif ($Optional) { 'Optional' } else { 'MISSING' })
        Detail = $(if ($ok) { $info } else { $fix })
    }
}

function Get-Checks {
    $serverModule = Join-Path $serverDir 'Modules\CommanderBattle'
    $gameModule = Join-Path $gameDir 'Modules\CommanderBattle'
    $serverDll = Join-Path $serverModule 'bin\Win64_Shipping_Server\CommanderBattle.dll'
    # The build and the release zip put the server DLL into the game's module too, so the two can be compared.
    $gameServerDll = Join-Path $gameModule 'bin\Win64_Shipping_Server\CommanderBattle.dll'
    $sameBuild = (Test-Path $serverDll) -and (Test-Path $gameServerDll) -and ((Get-FileHash $serverDll).Hash -eq (Get-FileHash $gameServerDll).Hash)
    $scenes = @(Get-ChildItem (Join-Path $serverModule 'SceneObj') -Directory -ErrorAction SilentlyContinue).Count
    $hasConfig = Test-Path $config
    $hasPassword = [bool](Get-ConfigValue AdminPassword)
    $hasToken = $env:CB_SERVER_TOKEN -or (Test-Path (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Tokens\DedicatedCustomServerAuthToken.txt'))

    New-Check 'Dedicated server' (Test-Path (Join-Path $serverDir 'bin\Win64_Shipping_Server\TaleWorlds.Starter.DotNetCore.exe')) 'Install "Mount & Blade II: Dedicated Server" (Steam > Library > Tools) next to the game.' $serverDir
    New-Check 'Mod in the game' (Test-Path (Join-Path $gameModule 'bin\Win64_Shipping_Client\CommanderBattle.dll')) 'Click Build, or unzip the release into the game''s Modules folder.'
    New-Check 'Mod on the server' (Test-Path $serverDll) 'Click Build, or unzip the release into the server''s Modules folder.'
    New-Check 'Game and server: same build' $sameBuild 'Click Build, or unzip the same release into both Modules folders.'
    New-Check 'Maps on the server' ($scenes -gt 0) 'Click Install maps.' "$scenes maps"
    New-Check 'Server config' $hasConfig 'Click Install maps.'
    New-Check 'Admin password' $hasPassword 'None set: nobody gets the admin panel. Type one above and click Save settings.' 'Set' -Optional
    New-Check 'Server token' ([bool]$hasToken) 'In the game: multiplayer lobby, Alt+~, then run customserver.gettoken'
    New-Check '.NET SDK (for Build)' ([bool](Get-Command dotnet -ErrorAction SilentlyContinue)) 'Only needed to build. Install the .NET SDK.' -Optional
    New-Check 'RBM source (for Build)' (Test-Path (Join-Path $repoDir '..\RealisticBattleProject')) 'Only needed to build. Clone it as Modules\RealisticBattleProject.' -Optional
}

if ($Check) {
    Get-StatusText
    Get-Checks | Format-Table -AutoSize -Wrap
    return
}

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Commander Battle Server'
$form.ClientSize = New-Object System.Drawing.Size 660, 688
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false

function Add-Control([string]$type, [string]$text, [int]$x, [int]$y, [int]$w, [int]$h) {
    $control = New-Object "System.Windows.Forms.$type"
    if ($text) { $control.Text = $text }
    $control.SetBounds($x, $y, $w, $h)
    $form.Controls.Add($control)
    $control
}

$status = Add-Control Label '' 12 10 636 48
$status.Font = New-Object System.Drawing.Font $form.Font, 'Bold'

$startButton = Add-Control Button 'Start / Restart' 12 64 120 28
$stopButton = Add-Control Button 'Stop' 140 64 120 28
$buildButton = Add-Control Button 'Build' 268 64 120 28
$mapsButton = Add-Control Button 'Install maps' 396 64 120 28
$logsButton = Add-Control Button 'Open logs' 524 64 124 28

# The fields show what is in the config; an empty password means none.
$null = Add-Control Label 'Server name:' 12 107 100 20
$serverName = Add-Control TextBox '' 116 104 272 22
$null = Add-Control Label 'Join password:' 12 135 100 20
$gamePassword = Add-Control TextBox '' 116 132 272 22
$null = Add-Control Label 'Admin password:' 12 163 100 20
$adminPassword = Add-Control TextBox '' 116 160 272 22
# Off is the Light mode: the multiplayer gear with a light damage curve.
$rbmCombat = Add-Control CheckBox 'RBM Combat' 400 104 240 22
$mapVoting = Add-Control CheckBox 'Vote for the next map after a match' 400 132 240 22
$mapPolls = Add-Control CheckBox 'Players can call a map-change poll' 400 160 240 22

$null = Add-Control Label 'Team 1:' 12 195 100 20
$culture1 = Add-Control ComboBox '' 116 192 110 22
$null = Add-Control Label 'Team 2:' 238 195 50 20
$culture2 = Add-Control ComboBox '' 290 192 98 22
foreach ($combo in $culture1, $culture2) {
    $combo.DropDownStyle = 'DropDownList'
    $combo.Items.AddRange(@('vlandia', 'empire', 'khuzait', 'aserai', 'sturgia', 'battania'))
}

# The limits are the game's (MultiplayerOptions): a round 60-3600 s, preparation 2-60 s, 1-99 rounds.
function Add-Number([int]$x, [int]$y, [int]$minimum, [int]$maximum) {
    $number = Add-Control NumericUpDown '' $x $y 60 22
    $number.Minimum = $minimum
    $number.Maximum = $maximum
    $number
}
$null = Add-Control Label 'Round (minutes):' 12 223 100 20
$roundMinutes = Add-Number 116 220 1 60
$null = Add-Control Label 'Rounds per map:' 196 223 92 20
$roundTotal = Add-Number 290 220 1 99
$null = Add-Control Label 'Preparation (s):' 12 251 100 20
$preparation = Add-Number 116 248 2 60
$saveButton = Add-Control Button 'Save settings' 400 216 120 28

function Get-ConfigOr([string]$option, $default) {
    $value = Get-ConfigValue $option
    if ($value) { $value } else { $default }
}

# A value outside the field's limits (a hand-edited config) is shown as the nearest limit.
function Set-Number($number, [int]$value) {
    $number.Value = [Math]::Min($number.Maximum, [Math]::Max($number.Minimum, $value))
}

function Read-Settings {
    $rbmCombat.Checked = Test-RbmCombat
    $serverName.Text = Get-ConfigValue ServerName
    $gamePassword.Text = Get-ConfigValue GamePassword
    $adminPassword.Text = Get-ConfigValue AdminPassword
    $culture1.SelectedItem = Get-ConfigOr CultureTeam1 'vlandia'
    $culture2.SelectedItem = Get-ConfigOr CultureTeam2 'battania'
    Set-Number $roundMinutes ([Math]::Round([int](Get-ConfigOr RoundTimeLimit 900) / 60))
    Set-Number $roundTotal (Get-ConfigOr RoundTotal 2)
    Set-Number $preparation (Get-ConfigOr RoundPreparationTimeLimit 45)
    $mapPolls.Checked = (Get-ConfigOr AllowPollsToChangeMaps 'True') -ne 'False'
    # On in a new config, as Install-CampaignScenes.ps1 writes it.
    $mapVoting.Checked = (-not (Test-Path $config)) -or [bool](Select-String -Path $config -Pattern '^enable_map_voting\s*$' -Quiet)
}

$checks = Add-Control ListView '' 12 282 636 212
$checks.View = 'Details'
$checks.FullRowSelect = $true
$checks.HeaderStyle = 'Nonclickable'
$checks.ShowItemToolTips = $true
$null = $checks.Columns.Add('Check', 190)
$null = $checks.Columns.Add('State', 70)
$null = $checks.Columns.Add('What to do', 355)

$output = Add-Control TextBox '' 12 504 636 172
$output.Multiline = $true
$output.ReadOnly = $true
$output.ScrollBars = 'Vertical'
$output.Font = New-Object System.Drawing.Font 'Consolas', 9

function Update-View {
    # The server's logs are being written while they are read: a failed read just waits for the next tick.
    try {
        $status.Text = Get-StatusText
        $checks.BeginUpdate()
        $checks.Items.Clear()
        foreach ($check in Get-Checks) {
            $item = $checks.Items.Add($check.Name)
            $null = $item.SubItems.Add($check.State)
            $null = $item.SubItems.Add($check.Detail)
            $item.ToolTipText = $check.Detail
            $item.ForeColor = switch ($check.State) { 'OK' { 'DarkGreen' } 'MISSING' { 'Firebrick' } default { 'DarkGoldenrod' } }
        }
        $checks.EndUpdate()
    }
    catch { $status.Text = "Could not read the state: $_" }
}

function Quote([string]$path) { "'" + $path.Replace("'", "''") + "'" }

# A long job (build, start) runs in its own console window, which stays open until Enter so its output can be read.
function Start-Console([string]$command) {
    $wrapped = "try { $command } catch { Write-Host `$_ -ForegroundColor Red }; Read-Host 'Done. Press Enter to close'"
    Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "`"$wrapped`""
}

# A short job runs here, with its output in the box below.
function Invoke-Inline([scriptblock]$job, [string]$note) {
    $form.Cursor = 'WaitCursor'
    try { $text = (& $job *>&1 | Out-String).Trim() } catch { $text = "FAILED: $_" }
    $form.Cursor = 'Default'
    $output.Text = ($text, $note | Where-Object { $_ }) -join "`r`n"
    Update-View
}

function Confirm-Disconnect {
    (-not (Get-Server)) -or ([System.Windows.Forms.MessageBox]::Show('The server is running. Players will be disconnected. Continue?', $form.Text, 'YesNo', 'Warning') -eq 'Yes')
}

$startButton.Add_Click({
    if (Confirm-Disconnect) { Start-Console "& $(Quote $startScript) -Restart" }
})
$stopButton.Add_Click({
    if (Confirm-Disconnect) { Invoke-Inline { & $startScript -Stop } }
})
$buildButton.Add_Click({
    # The server locks its DLLs, so it is stopped first.
    if (Confirm-Disconnect) {
        Start-Console "& $(Quote $startScript) -Stop; dotnet build $(Quote (Join-Path $repoDir 'CommanderBattle.sln')) -c Release -v:minimal; if (`$LASTEXITCODE) { Write-Host 'BUILD FAILED. Is the game still open? It locks the client DLLs.' -ForegroundColor Red }"
    }
})
$mapsButton.Add_Click({
    Invoke-Inline { & $installScript } 'Restart the server to use a changed config.'
})
$saveButton.Add_Click({
    Invoke-Inline {
        & $installScript -ServerName $serverName.Text.Trim() -GamePassword $gamePassword.Text.Trim() -AdminPassword $adminPassword.Text.Trim() `
            -CultureTeam1 $culture1.SelectedItem -CultureTeam2 $culture2.SelectedItem `
            -RoundTimeLimit ([int]$roundMinutes.Value * 60) -RoundTotal ([int]$roundTotal.Value) -RoundPreparationTimeLimit ([int]$preparation.Value) `
            -AllowPollsToChangeMaps "$($mapPolls.Checked)" -MapVoting "$($mapVoting.Checked)"
        # Written only on a change, so a file the host edited by hand keeps its comments.
        if ($rbmCombat.Checked -ne (Test-RbmCombat)) {
            Set-Content -Path $combatFile -Value $(if ($rbmCombat.Checked) { 'RBM' } else { 'Light' })
            "Combat mode: $(Get-Content $combatFile)"
        }
    } 'Restart the server: it reads these settings when it starts.'
    Read-Settings
})
$logsButton.Add_Click({ Start-Process explorer.exe $logDir })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 4000
$timer.Add_Tick({ Update-View })
$timer.Start()

Update-View
Read-Settings
if ($Snapshot) {
    $form.Add_Shown({
        $bitmap = New-Object System.Drawing.Bitmap $form.Width, $form.Height
        $form.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle 0, 0, $form.Width, $form.Height))
        $bitmap.Save($Snapshot)
        $form.Close()
    })
}
[void]$form.ShowDialog()
$timer.Dispose()
