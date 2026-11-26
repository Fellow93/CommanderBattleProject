<#
.SYNOPSIS
    A small window for the host: is the server running, is everything installed, and buttons to start, stop, build,
    install the maps and set the admin password.

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

function Get-StatusText {
    $server = Get-Server
    if (-not $server) { return 'Server: stopped' }
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
    $text
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
    $hasPassword = $hasConfig -and [bool](Select-String -Path $config -Pattern '^AdminPassword\s+\S' -Quiet)
    $hasToken = $env:CB_SERVER_TOKEN -or (Test-Path (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Tokens\DedicatedCustomServerAuthToken.txt'))

    New-Check 'Dedicated server' (Test-Path (Join-Path $serverDir 'bin\Win64_Shipping_Server\TaleWorlds.Starter.DotNetCore.exe')) 'Install "Mount & Blade II: Dedicated Server" (Steam > Library > Tools) next to the game.' $serverDir
    New-Check 'Mod in the game' (Test-Path (Join-Path $gameModule 'bin\Win64_Shipping_Client\CommanderBattle.dll')) 'Click Build, or unzip the release into the game''s Modules folder.'
    New-Check 'Mod on the server' (Test-Path $serverDll) 'Click Build, or unzip the release into the server''s Modules folder.'
    New-Check 'Game and server: same build' $sameBuild 'Click Build, or unzip the same release into both Modules folders.'
    New-Check 'Maps on the server' ($scenes -gt 0) 'Click Install maps.' "$scenes maps"
    New-Check 'Server config' $hasConfig 'Click Install maps.'
    New-Check 'Admin password' $hasPassword 'None set: nobody gets the admin panel. Type one above and click Set password.' 'Set' -Optional
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
$form.ClientSize = New-Object System.Drawing.Size 660, 600
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false

function Add-Control([string]$type, [string]$text, [int]$x, [int]$y, [int]$w, [int]$h) {
    $control = New-Object "System.Windows.Forms.$type"
    $control.Text = $text
    $control.SetBounds($x, $y, $w, $h)
    $form.Controls.Add($control)
    $control
}

$status = Add-Control Label '' 12 10 636 40
$status.Font = New-Object System.Drawing.Font $form.Font, 'Bold'

$startButton = Add-Control Button 'Start / Restart' 12 56 120 28
$stopButton = Add-Control Button 'Stop' 140 56 120 28
$buildButton = Add-Control Button 'Build' 268 56 120 28
$mapsButton = Add-Control Button 'Install maps' 396 56 120 28
$logsButton = Add-Control Button 'Open logs' 524 56 124 28

$null = Add-Control Label 'Admin password:' 12 100 100 20
$password = Add-Control TextBox '' 116 97 272 22
$passwordButton = Add-Control Button 'Set password' 396 94 120 28

$checks = Add-Control ListView '' 12 134 636 262
$checks.View = 'Details'
$checks.FullRowSelect = $true
$checks.HeaderStyle = 'Nonclickable'
$checks.ShowItemToolTips = $true
$null = $checks.Columns.Add('Check', 190)
$null = $checks.Columns.Add('State', 70)
$null = $checks.Columns.Add('What to do', 355)

$output = Add-Control TextBox '' 12 406 636 182
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
$passwordButton.Add_Click({
    # An empty password removes it, so that is asked first.
    if (-not $password.Text -and [System.Windows.Forms.MessageBox]::Show('Remove the admin password?', $form.Text, 'YesNo') -ne 'Yes') { return }
    Invoke-Inline { & $installScript -AdminPassword $password.Text } 'Restart the server: it hands the password to the lobby when it starts.'
    $password.Text = ''
})
$logsButton.Add_Click({ Start-Process explorer.exe $logDir })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 4000
$timer.Add_Tick({ Update-View })
$timer.Start()

Update-View
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
