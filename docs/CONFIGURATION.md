# Configuration Guide

XPX reads its live settings from `XPXLevels.json`.

This document explains the main config sections and what they do.

## Basic Settings

Core progression keys:

- `ChatPrefix`
- `ServerName`
- `MaxLevel`
- `BaseXpToLevel`
- `XpLinearGrowthPerLevel`
- `XpQuadraticGrowthPerLevel`

These define:

- the server branding used in chat
- the level cap
- the shape of the XP curve

## XP and Credits

XP reward keys:

- `CasualCompetitiveKillXp`
- `FastModeKillXp`
- `KnifeKillBonusXp`
- `HeadshotBonusXp`
- `RoundWinXp`
- `BombPlantXp`
- `BombDefuseXp`
- `AssistXp`
- `MvpXp`
- `ClutchXp`
- `FirstBloodXp`
- `ChickenKillXp`
- `BotXpMultiplier`

Credit reward keys:

- `CasualCompetitiveKillCredits`
- `FastModeKillCredits`
- `RoundWinCredits`
- `AssistCredits`
- `MvpCredits`
- `FirstBloodCredits`
- `ClutchCredits`

Other reward behavior:

- `ShowKillXpMessages`

## Gamble

- `GambleWinChancePercent`
- `GambleMinXp`
- `GambleMaxXp`
- `GambleCooldownSeconds`

## RTV and Voting

- `RtvRequiredRatio`
- `RtvVoteDurationSeconds`
- `RtvReminderSeconds`
- `RtvMapOptionCount`
- `MapChangeDelaySeconds`

## Welcome and Join Flow

`WelcomeMessages` supports token expansion:

- `{NAME}`
- `{SERVER}`
- `{LEVEL}`
- `{TOTAL_XP}`
- `{NEXT_LEVEL}`
- `{XP_NEEDED}`
- `{NEXT_REWARD}`

## Level Rewards

`Rewards` is the level reward ladder.

Each reward entry can include:

- `Level`
- `Tag`
- `KnifeItem`

Example:

```json
{
  "Level": 100,
  "Tag": "[NEBULA]",
  "KnifeItem": "weapon_knife_flip"
}
```

## Missions

`DailyMissions` and `WeeklyMissions` use:

- `Key`
- `Title`
- `Description`
- `Scope`
- `Objective`
- `Goal`
- `RewardXp`
- `RewardCredits`

Common objectives:

- `Kills`
- `Headshots`
- `Wins`
- `KnifeKills`
- `Assists`
- `Mvps`
- `BombPlants`
- `BombDefuses`
- `BombObjectives`
- `FirstBloods`
- `ClutchWins`
- `PlayMinutes`
- `CratesOpened`

## Achievements

Each achievement supports:

- `Key`
- `Title`
- `Description`
- `Badge`
- `Objective`
- `Goal`
- `RewardCredits`

Achievements are permanent and tracked by SteamID.

## Shop

`ShopItems` currently support these reward types:

- `Xp`
- `Credits`
- `CrateToken`
- `XpBoost`

Each item includes:

- `Key`
- `Name`
- `Description`
- `RewardType`
- `RewardAmount`
- `CostCredits`

## Crates

`Crates` define the crate catalog. Each crate contains weighted `Rewards`.

Crate reward fields:

- `Label`
- `Rarity`
- `RewardType`
- `RewardAmount`
- `DurationMinutes`
- `Weight`

Supported rarities:

- `Common`
- `Rare`
- `Epic`
- `Legendary`

Supported reward types:

- `Xp`
- `Credits`
- `CrateToken`
- `XpBoost`

Example reward:

```json
{
  "Label": "20% XP Boost",
  "Rarity": "Epic",
  "RewardType": "XpBoost",
  "RewardAmount": 20,
  "DurationMinutes": 10,
  "Weight": 4
}
```

## Maps

Normal maps go in:

- `MapPool`

Workshop maps go in:

- `WorkshopMaps`

Workshop entries use:

- `Id`
- `Label`

Example:

```json
{
  "Id": "3354923062",
  "Label": "Manor"
}
```

## Admin Menu Presets

- `AdminXpAmounts`
- `GameModes`

`AdminXpAmounts` controls the quick-select amounts shown in the admin XP / Credits flow.

`GameModes` defines the available game mode aliases in the admin mode switcher.

## Editing Safely

Recommended workflow:

1. edit `XPXLevelsConfig.cs` if you want to change source defaults
2. edit the live `XPXLevels.json` for server-specific settings
3. redeploy the plugin
4. re-test the affected commands or menus

When in doubt, keep live tuning inside JSON and reserve C# changes for behavior changes.
