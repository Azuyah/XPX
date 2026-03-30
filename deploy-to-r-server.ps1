param(
    [string]$Configuration = "Debug",
    [string]$ServerRoot = "R:\cs2-ds"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildOutput = Join-Path $projectRoot "bin\$Configuration\net8.0"
$cssRoot = Join-Path $ServerRoot "game\csgo\addons\counterstrikesharp"
$pluginTargetRoot = Join-Path $cssRoot "plugins"
$pluginTarget = Join-Path $pluginTargetRoot "XPXLevels"
$configTargetRoot = Join-Path $cssRoot "configs\plugins"
$configTarget = Join-Path $configTargetRoot "XPXLevels"
$configFile = Join-Path $configTarget "XPXLevels.json"
$dataTarget = Join-Path $cssRoot "data\XPXLevels"
$dataFile = Join-Path $dataTarget "xpx-levels.db"
$startScript = Join-Path $ServerRoot "start-cs2-server.bat"

Write-Host "Building XPXLevels ($Configuration)..."
dotnet build (Join-Path $projectRoot "XPXLevels.csproj") -c $Configuration

if (!(Test-Path $buildOutput)) {
    throw "Build output not found: $buildOutput"
}

if (!(Test-Path $pluginTargetRoot)) {
    throw "CounterStrikeSharp plugin directory not found: $pluginTargetRoot"
}

Write-Host "Stopping CS2 server..."
Get-Process | Where-Object { $_.ProcessName -eq "cs2" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

New-Item -ItemType Directory -Path $dataTarget -Force | Out-Null
New-Item -ItemType Directory -Path $configTarget -Force | Out-Null

$legacyPluginTarget = Get-ChildItem $pluginTargetRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -ne "XPXLevels" -and
        (Get-ChildItem $_.FullName -File -Filter "*levels.db" -ErrorAction SilentlyContinue | Select-Object -First 1)
    } |
    Select-Object -First 1 -ExpandProperty FullName

$legacyConfigTarget = Get-ChildItem $configTargetRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -ne "XPXLevels" -and
        (Get-ChildItem $_.FullName -File -Filter "*Levels.json" -ErrorAction SilentlyContinue | Select-Object -First 1)
    } |
    Select-Object -First 1 -ExpandProperty FullName

$legacyConfigFile = if ($legacyConfigTarget) {
    Get-ChildItem $legacyConfigTarget -File -Filter "*Levels.json" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

if (!(Test-Path $dataFile)) {
    $legacyDbCandidates = @((Join-Path $pluginTarget "xpx-levels.db"), (Join-Path $pluginTarget "XPX-levels.db"))

    if ($legacyPluginTarget) {
        $legacyDbCandidates += Get-ChildItem $legacyPluginTarget -File -Filter "*levels.db" -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }

    foreach ($candidate in $legacyDbCandidates) {
        if (Test-Path $candidate) {
            Copy-Item -LiteralPath $candidate -Destination $dataFile -Force
            break
        }
    }
}

if (!(Test-Path $configFile) -and (Test-Path $legacyConfigFile)) {
    Copy-Item -LiteralPath $legacyConfigFile -Destination $configFile -Force
}

if (Test-Path $configFile) {
    $configRaw = Get-Content -LiteralPath $configFile -Raw
    $configJson = (($configRaw -split "`r?`n") | Where-Object { $_ -notmatch '^\s*//' }) -join [Environment]::NewLine
    $config = $configJson | ConvertFrom-Json
    $config.ChatPrefix = "{Green}[XPX]{Default}"
    $config.ServerName = "XPX CS2"
    $config.KickReason = "Removed by an XPX admin."

    if ($config.Rewards) {
        foreach ($reward in $config.Rewards) {
            if ($reward.Level -eq 50 -and $reward.PSObject.Properties.Match('Tag').Count -gt 0) {
                $reward.Tag = "[XPX]"
            }
        }
    }

    $config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $configFile
}

if (Test-Path $pluginTarget) {
    Remove-Item -LiteralPath $pluginTarget -Recurse -Force
}

if ($legacyPluginTarget -and (Test-Path $legacyPluginTarget)) {
    Remove-Item -LiteralPath $legacyPluginTarget -Recurse -Force
}

Write-Host "Copying plugin files to $pluginTarget..."
New-Item -ItemType Directory -Path $pluginTarget | Out-Null
Copy-Item -Path (Join-Path $buildOutput "*") -Destination $pluginTarget -Recurse -Force

if ($legacyConfigTarget -and (Test-Path $legacyConfigTarget)) {
    Remove-Item -LiteralPath $legacyConfigTarget -Recurse -Force
}

Write-Host "Starting CS2 server..."
Start-Process -FilePath $startScript -WorkingDirectory $ServerRoot

Write-Host "Done."
