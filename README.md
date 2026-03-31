# XPX

> Persistent progression, missions, economy, and admin tooling for serious CS2 community servers.

![Counter-Strike 2](https://img.shields.io/badge/Counter--Strike_2-CS2-f28c28)
![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.364-1f6feb)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)
![SQLite](https://img.shields.io/badge/Storage-SQLite-003B57?logo=sqlite)
![License](https://img.shields.io/badge/License-Proprietary-red)

![CS2 XPX Banner](docs/screenshots/header-cs2-xpx.png)

XPX is a CounterStrikeSharp plugin for Counter-Strike 2 that adds a full server-side progression layer:

- level and XP progression up to level `500`
- rank, top list, tags, and level rewards
- persistent SQLite-backed player data
- missions, achievements, stats, economy, shop, crates, and XP boosts
- RTV and admin map voting
- admin tools for maps, modes, XP, credits, bots, and special rounds
- forced global loadout modes for rifle, pistol, or knife-only servers

The plugin is designed for self-hosted CS2 community servers and is currently deployed with a local Windows + SteamCMD setup.

## License and Usage

XPX is source-available and proprietary.

- personal or internal server use is allowed
- redistribution, resale, sublicensing, repackaging, and public reposting are not allowed
- modified copies must not remove or obscure the original authorship, branding, or attribution
- derivative versions must not be presented as an unrelated original work

See [LICENSE.md](LICENSE.md) for the full notice.

## Preview

![Preview Banner](docs/screenshots/banner-preview.png)

| Player Hub | Admin Menu | Shop / Crates |
| --- | --- | --- |
| ![Player Hub](docs/screenshots/me.png) | ![Admin Menu](docs/screenshots/admin-page-1.png) | ![Shop](docs/screenshots/shop.png) |

| Help | Missions | Crate Reward |
| --- | --- | --- |
| ![Help](docs/screenshots/help.png) | ![Missions](docs/screenshots/missions.png) | ![Crate Reward](docs/screenshots/crate-reward.png) |

| Level Up | Admin Page 2 | Chat Progression |
| --- | --- | --- |
| ![Level Up](docs/screenshots/level-up.png) | ![Admin Page 2](docs/screenshots/admin-page-3.png) | ![Chat Progression](docs/screenshots/chat-text.png) |

## What XPX Includes

![Includes Banner](docs/screenshots/banner-includes.png)

### Core progression

- XP from kills, wins, bomb plants, bomb defuses, assists, MVPs, first blood, clutches, killstreaks, and multikills
- separate rewards for normal modes and fast modes such as Deathmatch / Arms Race
- bot XP scaling
- warmup XP disabled
- persistent level, tag, and reward state per SteamID

### Player systems

- `!me`, `!rank`, `!level`, `!top`
- `!rewards` to view the full level reward ladder
- `!stats` with K/D, HS%, playtime, favorite weapon, streaks, and economy state
- `!missions` for daily and weekly mission progress
- `!achievements` for permanent badge unlocks
- `!shop`, `!wallet`, and `!inventory`
- `!gamble <xp>`

### Economy and crates

- server-side Credits economy
- shop purchases for XP and crate tokens
- weighted crate drops with `Common`, `Rare`, `Epic`, and `Legendary` tiers
- crate rewards are limited to XP, Credits, crate tokens, and temporary XP boosts
- active boosts persist and expire automatically

### Server and admin tools

- `!rtv` / `!vote` map voting
- map and workshop map rotation
- admin menu with map, mode, vote, bot, XP, and credit actions
- special rounds such as knife rounds, pistol rounds, and warmup events
- forced server-wide loadout mode for all players
- global toggle for progression notifications

## Requirements

- Counter-Strike 2 dedicated server
- CounterStrikeSharp
- .NET 8 SDK for local builds
- Windows PowerShell if you want to use the included deploy script as-is

NuGet packages used by this project:

- `CounterStrikeSharp.API 1.0.364`
- `Dapper 2.1.72`
- `Microsoft.Data.Sqlite 10.0.5`

## Install Options

![Install Banner](docs/screenshots/banner-install-bind.png)

### Manual install from a ZIP

Use one of these:

1. download a provided release ZIP if you have access to the release page
2. or get a plugin ZIP directly from the maintainer
3. extract it locally
4. copy the plugin files into your CS2 CounterStrikeSharp plugin folder

Recommended current package name:

```text
XPX-v1.0.2-plugin.zip
```

See [docs/SETUP.md](docs/SETUP.md) for the manual install path.

## Quick Start

### 1. Deploy the plugin files

Copy the extracted plugin build into your server's CounterStrikeSharp plugin folder.

### 2. Deploy it

If your server layout matches the current local setup, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy-to-r-server.ps1
```

By default the deploy script targets:

- server root: `R:\cs2-ds`
- plugin path: `R:\cs2-ds\game\csgo\addons\counterstrikesharp\plugins\XPXLevels`
- config path: `R:\cs2-ds\game\csgo\addons\counterstrikesharp\configs\plugins\XPXLevels\XPXLevels.json`
- data path: `R:\cs2-ds\game\csgo\addons\counterstrikesharp\data\XPXLevels`

### 3. Join the server

Typical local connect:

```text
connect 127.0.0.1:27015
```

## Customize XPX

After the first server start, open this file:

```text
game\csgo\addons\counterstrikesharp\configs\plugins\XPXLevels\XPXLevels.json
```

Most common edits:

- `Rewards`: change tags and knife unlocks
- `BaseXpToLevel`, `XpLinearGrowthPerLevel`, `XpQuadraticGrowthPerLevel`: tune the XP curve
- `CasualCompetitiveKillXp`, `FastModeKillXp`, `KnifeKillBonusXp`, `HeadshotBonusXp`: tune combat rewards
- `WelcomeMessages`: change the join text players see

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for the quick field guide.

## Menu Keybinds

Use `!bindmenu` in chat first. XPX will try to bind the menu keys locally.

XPX menu controls:

- `1-6` select visible items
- `7` is `Back` on page 1 or `Prev` on later pages
- `8` is `Next`
- `9` is `Close`

If `!bindmenu` does not stick on your client, use these binds manually in the CS2 console:

```cfg
bind 1 "slot1;css_1"
bind 2 "slot2;css_2"
bind 3 "slot3;css_3"
bind 4 "slot4;css_4"
bind 5 "slot5;css_5"
bind 6 "slot6;css_6"
bind 7 "slot7;css_7"
bind 8 "slot8;css_8"
bind 9 "slot9;css_9"
host_writeconfig
```

If you do not bind the keys, XPX still supports chat fallback with `!1` through `!9` while a menu is open.

## Main Commands

![Commands Banner](docs/screenshots/banner-commands.png)

### Player commands

- `!me`
- `!help`
- `!commands`
- `!rewards`
- `!level`
- `!rank`
- `!top`
- `!stats`
- `!missions`
- `!achievements`
- `!shop`
- `!wallet`
- `!inventory` / `!inv`
- `!rtv`
- `!vote`
- `!gamble <xp>`

### Admin commands

- `!admin`
- `css_givexp`
- `css_removexp`
- `css_givecredits`
- `css_removecredits`
- `css_changemap`
- `css_restartmap`
- `css_setmode`
- `css_kick`
- `css_kickbots`
- `css_addbots`
- `css_forceloadout`
- `css_forcevote`
- `css_cancelvote`
- `css_kniferound`
- `css_pistolround`
- `css_warmupevent`

For the complete command reference, see [docs/COMMANDS.md](docs/COMMANDS.md).

## Configuration

XPX is driven by `XPXLevels.json`. The config covers:

- XP curve and max level
- XP rewards and credits rewards
- mission definitions
- achievement definitions
- shop items
- crate definitions
- map pool and workshop map pool
- admin XP quick-amount presets
- level reward ladder
- welcome messages

For a detailed config guide, see [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Persistence

XPX stores player progression in SQLite by SteamID. It persists:

- XP
- level-derived state
- Credits
- crate tokens
- XP boosts
- stats
- missions
- achievements

The live DB path used by the deploy script is:

```text
game\csgo\addons\counterstrikesharp\data\XPXLevels\xpx-levels.db
```

XPX also keeps a short-lived transition snapshot during map changes to protect player XP from map-switch reload edge cases.

## Repository Layout

```text
XPXLevels.csproj
XPXLevelsPlugin.cs       # core plugin flow, menus, XP, map vote, admin tools
XPXLevelsFeatures.cs     # stats, missions, achievements, shop, crates, special rounds
XPXLevelsConfig.cs       # config schema and default values
XPXLevelsModels.cs       # shared models and enums
XPXLevelsRepository.cs   # SQLite persistence layer
XPXNumberMenu.cs         # custom center-screen numbered menu renderer
deploy-to-r-server.ps1   # local build + deploy + restart helper
docs/
```

## Development

Common local workflow:

```powershell
dotnet build
powershell -ExecutionPolicy Bypass -File .\deploy-to-r-server.ps1
git status
git add .
git commit -m "Describe the change"
git push origin main
```

More detail lives in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Additional Docs

- [docs/SETUP.md](docs/SETUP.md)
- [docs/COMMANDS.md](docs/COMMANDS.md)
- [docs/CONFIGURATION.md](docs/CONFIGURATION.md)
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
- [docs/releases/v1.0.2.md](docs/releases/v1.0.2.md)
- [docs/releases/v1.0-xponly.md](docs/releases/v1.0-xponly.md)

## Notes

- XPX uses custom numbered center menus with `1-9`, plus `!1-!9` chat fallback.
- Workshop maps are supported by workshop ID.
- Local non-workshop custom maps require separate client distribution and are not the preferred path.
- The project currently targets a Windows-hosted local CS2 dedicated server workflow.
