# Setup Guide

This guide explains how to build, install, and run XPX on a CS2 dedicated server.

## Requirements

- CS2 dedicated server installed with SteamCMD
- CounterStrikeSharp installed on the server
- .NET 8 SDK on the development machine
- access to the server files

## Project Build

From the repo root:

```powershell
dotnet build
```

Expected output:

```text
bin\Debug\net8.0\XPXLevels.dll
```

## Deploy Script

The repo includes a Windows deployment helper:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy-to-r-server.ps1
```

By default the script:

1. builds the plugin
2. stops the running CS2 server process
3. preserves the XPX data directory
4. copies the latest plugin build into the server plugin folder
5. restores / migrates config and DB files if needed
6. starts the CS2 server again

The default server root in the script is:

```text
R:\cs2-ds
```

If your server lives somewhere else, edit the `ServerRoot` parameter or pass it in directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy-to-r-server.ps1 -ServerRoot "D:\my-cs2-server"
```

## Plugin Paths

The deploy script expects the following CounterStrikeSharp layout:

```text
game\csgo\addons\counterstrikesharp\plugins\XPXLevels
game\csgo\addons\counterstrikesharp\configs\plugins\XPXLevels
game\csgo\addons\counterstrikesharp\data\XPXLevels
```

Important files:

- plugin DLL folder:
  `game\csgo\addons\counterstrikesharp\plugins\XPXLevels`
- config file:
  `game\csgo\addons\counterstrikesharp\configs\plugins\XPXLevels\XPXLevels.json`
- SQLite DB:
  `game\csgo\addons\counterstrikesharp\data\XPXLevels\xpx-levels.db`
- transition snapshot:
  `game\csgo\addons\counterstrikesharp\data\XPXLevels\transition-snapshot.json`

## First Server Test

After deployment:

1. start or restart the server
2. join the server
3. run `!me`
4. run `!help`
5. get a kill and verify XP gain
6. change map and verify XP persistence with `!level`

## Admin Access

XPX uses CounterStrikeSharp permissions. Make sure your SteamID is configured in your server admin files and granted the XPX admin groups used by the plugin.

The project has historically used these permission groups:

- `@XPX/root`
- `@XPX/menu`
- `@XPX/xp`
- `@XPX/map`
- `@XPX/kick`
- `@XPX/vote`

## Workshop Maps

For best compatibility, use Workshop maps by Workshop ID in the `WorkshopMaps` config section.

Why:

- clients can fetch Workshop-hosted content more reliably
- the server can launch them with workshop-aware commands
- it avoids manual map distribution for most cases

## Troubleshooting

### Plugin does not load

- confirm CounterStrikeSharp is installed
- check the CSSharp logs under:
  `game\csgo\addons\counterstrikesharp\logs`
- rebuild with `dotnet build`

### Player progress does not save

- confirm `xpx-levels.db` exists in the XPX data folder
- check file permissions
- inspect the CSSharp logs for save/reload lines

### Custom map fails to load

- prefer Workshop maps instead of local-only VPK maps
- ensure the map exists in `MapPool` or `WorkshopMaps`
- verify the client can actually obtain the map data
