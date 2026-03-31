# Development Guide

This document covers the day-to-day workflow for maintaining XPX.

## Project Files

- `XPXLevelsPlugin.cs`
  Core gameplay logic, admin tools, map vote logic, menu flow, XP, loadouts, and persistence hooks.

- `XPXLevelsFeatures.cs`
  Stats, missions, achievements, shop, crates, boosts, and special round helpers.

- `XPXLevelsConfig.cs`
  Config schema and default values.

- `XPXLevelsModels.cs`
  Shared models and enums.

- `XPXLevelsRepository.cs`
  SQLite persistence and rank queries.

- `XPXNumberMenu.cs`
  Custom center-screen numbered menu renderer.

- `deploy-to-r-server.ps1`
  Build, deploy, and restart helper for the current Windows server setup.

## Build

```powershell
dotnet build
```

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy-to-r-server.ps1
```

The deploy script is opinionated toward the current local server layout. If you move the server, update `ServerRoot`.

## Persistence Model

XPX stores data in SQLite and persists by SteamID.

Main persisted areas:

- player progression
- stats
- weapon stats
- mission progress
- achievements

The repository also supports rank queries and top-player lookups.

## Map Change Safety

XPX keeps:

- regular DB saves
- disconnect saves
- periodic autosaves
- transition snapshots across map changes

That combination exists because CS2 player identity timing can be awkward during map transitions.

## Menu System

XPX uses a custom numbered center menu instead of a raw chat-driven flow.

Current conventions:

- `1-6` item selection
- `7` = `Back` on first page or `Prev` on later pages
- `8` = `Next`
- `9` = `Close`

If you add new menus:

1. keep labels short enough to stay on one line
2. use `ConfigureBackSlot(menu, ...)` for submenu navigation
3. avoid using `1` as a dedicated back button
4. watch total row count carefully when body text is present

## Recommended Change Workflow

1. edit source
2. build locally
3. deploy to the server
4. verify plugin load in the CSSharp log
5. test the affected command or menu in-game
6. commit
7. push

## Useful Commands

```powershell
git status
git add .
git commit -m "Your change"
git push origin main
```

```powershell
Get-Content R:\cs2-ds\game\csgo\addons\counterstrikesharp\logs\log-allYYYYMMDD.txt -Tail 80
```

## Logging

The fastest health check after deploy is the CSSharp log:

- `Finished loading plugin XPX Levels`

If that line appears after restart, the plugin loaded.

## Notes for Future Work

Good future additions usually fit one of these buckets:

- progression tuning
- new mission / achievement content
- economy balance
- admin QoL
- menu usability
- map flow improvements

Riskier areas include:

- client-side input assumptions
- spoofing inventory-style cosmetics
- local non-workshop custom map distribution
