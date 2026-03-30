using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace XPXLevels;

public sealed class XPXLevelsConfig : BasePluginConfig
{
    [JsonPropertyName("ChatPrefix")] public string ChatPrefix { get; set; } = "{Green}[XPX]{Default}";
    [JsonPropertyName("ServerName")] public string ServerName { get; set; } = "XPX CS2";
    [JsonPropertyName("MaxLevel")] public int MaxLevel { get; set; } = 500;
    [JsonPropertyName("BaseXpToLevel")] public int BaseXpToLevel { get; set; } = 475;
    [JsonPropertyName("XpLinearGrowthPerLevel")] public double XpLinearGrowthPerLevel { get; set; } = 6d;
    [JsonPropertyName("XpQuadraticGrowthPerLevel")] public double XpQuadraticGrowthPerLevel { get; set; } = 0.017d;
    [JsonPropertyName("CasualCompetitiveKillXp")] public int CasualCompetitiveKillXp { get; set; } = 75;
    [JsonPropertyName("FastModeKillXp")] public int FastModeKillXp { get; set; } = 25;
    [JsonPropertyName("KnifeKillBonusXp")] public int KnifeKillBonusXp { get; set; } = 25;
    [JsonPropertyName("HeadshotBonusXp")] public int HeadshotBonusXp { get; set; } = 10;
    [JsonPropertyName("RoundWinXp")] public int RoundWinXp { get; set; } = 30;
    [JsonPropertyName("BombPlantXp")] public int BombPlantXp { get; set; } = 20;
    [JsonPropertyName("BombDefuseXp")] public int BombDefuseXp { get; set; } = 25;
    [JsonPropertyName("AssistXp")] public int AssistXp { get; set; } = 15;
    [JsonPropertyName("MvpXp")] public int MvpXp { get; set; } = 25;
    [JsonPropertyName("ClutchXp")] public int ClutchXp { get; set; } = 40;
    [JsonPropertyName("FirstBloodXp")] public int FirstBloodXp { get; set; } = 15;
    [JsonPropertyName("CasualCompetitiveKillCredits")] public int CasualCompetitiveKillCredits { get; set; } = 2;
    [JsonPropertyName("FastModeKillCredits")] public int FastModeKillCredits { get; set; } = 1;
    [JsonPropertyName("RoundWinCredits")] public int RoundWinCredits { get; set; } = 3;
    [JsonPropertyName("AssistCredits")] public int AssistCredits { get; set; } = 1;
    [JsonPropertyName("MvpCredits")] public int MvpCredits { get; set; } = 2;
    [JsonPropertyName("FirstBloodCredits")] public int FirstBloodCredits { get; set; } = 1;
    [JsonPropertyName("ClutchCredits")] public int ClutchCredits { get; set; } = 4;
    [JsonPropertyName("ChickenKillXp")] public int ChickenKillXp { get; set; } = 1;
    [JsonPropertyName("BotXpMultiplier")] public double BotXpMultiplier { get; set; } = 0.30d;
    [JsonPropertyName("ShowKillXpMessages")] public bool ShowKillXpMessages { get; set; } = true;
    [JsonPropertyName("CurrencyName")] public string CurrencyName { get; set; } = "Credits";
    [JsonPropertyName("GambleWinChancePercent")] public int GambleWinChancePercent { get; set; } = 50;
    [JsonPropertyName("GambleMinXp")] public int GambleMinXp { get; set; } = 10;
    [JsonPropertyName("GambleMaxXp")] public int GambleMaxXp { get; set; } = 10000;
    [JsonPropertyName("GambleCooldownSeconds")] public int GambleCooldownSeconds { get; set; } = 15;
    [JsonPropertyName("RtvRequiredRatio")] public double RtvRequiredRatio { get; set; } = 0.60d;
    [JsonPropertyName("RtvVoteDurationSeconds")] public int RtvVoteDurationSeconds { get; set; } = 20;
    [JsonPropertyName("RtvReminderSeconds")] public int RtvReminderSeconds { get; set; } = 8;
    [JsonPropertyName("RtvMapOptionCount")] public int RtvMapOptionCount { get; set; } = 5;
    [JsonPropertyName("MapChangeDelaySeconds")] public int MapChangeDelaySeconds { get; set; } = 3;
    [JsonPropertyName("TopCount")] public int TopCount { get; set; } = 10;
    [JsonPropertyName("KickReason")] public string KickReason { get; set; } = "Removed by an XPX admin.";
    [JsonPropertyName("WelcomeMessages")] public List<string> WelcomeMessages { get; set; } =
    [
        "{Gold}Welcome back {White}{NAME}{Gold} to {White}{SERVER}{Gold}.",
        "{LightBlue}Use {White}!level {LightBlue}| {White}!rank {LightBlue}| {White}!top {LightBlue}| {White}!stats {LightBlue}| {White}!missions {LightBlue}| {White}!help {LightBlue}| {White}!me",
        "{Yellow}You are level {White}{LEVEL}{Yellow} with {White}{TOTAL_XP}{Yellow} XP. Next unlock: {White}{NEXT_REWARD}{Yellow}."
    ];

    [JsonPropertyName("KillstreakBonuses")] public List<KillstreakBonusOption> KillstreakBonuses { get; set; } =
    [
        new() { Threshold = 3, RewardXp = 10, RewardCredits = 1, Label = "Streak x3" },
        new() { Threshold = 5, RewardXp = 25, RewardCredits = 2, Label = "Streak x5" },
        new() { Threshold = 8, RewardXp = 45, RewardCredits = 4, Label = "Streak x8" }
    ];

    [JsonPropertyName("MultikillBonuses")] public List<MultikillBonusOption> MultikillBonuses { get; set; } =
    [
        new() { Kills = 2, RewardXp = 10, RewardCredits = 1, Label = "Double kill" },
        new() { Kills = 3, RewardXp = 20, RewardCredits = 2, Label = "Triple kill" },
        new() { Kills = 4, RewardXp = 35, RewardCredits = 3, Label = "Quad kill" },
        new() { Kills = 5, RewardXp = 60, RewardCredits = 5, Label = "Ace" }
    ];

    [JsonPropertyName("DailyMissions")] public List<MissionDefinition> DailyMissions { get; set; } =
    [
        new() { Key = "daily_headshots", Title = "Daily Headshots", Description = "Land 25 headshots", Scope = "daily", Objective = MissionObjective.Headshots, Goal = 25, RewardXp = 250, RewardCredits = 40 },
        new() { Key = "daily_wins", Title = "Daily Winner", Description = "Win 3 maps", Scope = "daily", Objective = MissionObjective.Wins, Goal = 3, RewardXp = 300, RewardCredits = 50 },
        new() { Key = "daily_knife_kills", Title = "Daily Blade", Description = "Get 5 knife kills", Scope = "daily", Objective = MissionObjective.KnifeKills, Goal = 5, RewardXp = 220, RewardCredits = 35 },
        new() { Key = "daily_objective", Title = "Daily Objective", Description = "Plant or defuse 4 bombs", Scope = "daily", Objective = MissionObjective.BombObjectives, Goal = 4, RewardXp = 240, RewardCredits = 35 },
        new() { Key = "daily_playtime", Title = "Daily Grind", Description = "Play for 30 minutes", Scope = "daily", Objective = MissionObjective.PlayMinutes, Goal = 30, RewardXp = 200, RewardCredits = 30 }
    ];

    [JsonPropertyName("WeeklyMissions")] public List<MissionDefinition> WeeklyMissions { get; set; } =
    [
        new() { Key = "weekly_kills", Title = "Weekly Slayer", Description = "Get 125 kills", Scope = "weekly", Objective = MissionObjective.Kills, Goal = 125, RewardXp = 1000, RewardCredits = 150 },
        new() { Key = "weekly_headshots", Title = "Weekly Sharpshooter", Description = "Land 60 headshots", Scope = "weekly", Objective = MissionObjective.Headshots, Goal = 60, RewardXp = 1000, RewardCredits = 150 },
        new() { Key = "weekly_wins", Title = "Weekly Closer", Description = "Win 10 maps", Scope = "weekly", Objective = MissionObjective.Wins, Goal = 10, RewardXp = 1200, RewardCredits = 180 },
        new() { Key = "weekly_assists", Title = "Weekly Support", Description = "Earn 25 assists", Scope = "weekly", Objective = MissionObjective.Assists, Goal = 25, RewardXp = 850, RewardCredits = 120 }
    ];

    [JsonPropertyName("Achievements")] public List<AchievementDefinition> Achievements { get; set; } =
    [
        new() { Key = "achievement_first_blood", Title = "Opening Move", Description = "Earn your first first blood", Badge = "OPENER", Objective = MissionObjective.FirstBloods, Goal = 1, RewardCredits = 20 },
        new() { Key = "achievement_100_headshots", Title = "Headhunter", Description = "Land 100 headshots", Badge = "HEADHUNTER", Objective = MissionObjective.Headshots, Goal = 100, RewardCredits = 75 },
        new() { Key = "achievement_25_knife_kills", Title = "Knife Artist", Description = "Get 25 knife kills", Badge = "KNIFE ARTIST", Objective = MissionObjective.KnifeKills, Goal = 25, RewardCredits = 75 },
        new() { Key = "achievement_250_kills", Title = "Frontliner", Description = "Get 250 kills", Badge = "FRONTLINER", Objective = MissionObjective.Kills, Goal = 250, RewardCredits = 100 },
        new() { Key = "achievement_25_mvps", Title = "MVP Machine", Description = "Earn 25 MVPs", Badge = "MVP MACHINE", Objective = MissionObjective.Mvps, Goal = 25, RewardCredits = 100 },
        new() { Key = "achievement_10_clutches", Title = "Clutch King", Description = "Win 10 clutch rounds", Badge = "CLUTCH KING", Objective = MissionObjective.ClutchWins, Goal = 10, RewardCredits = 120 }
    ];

    [JsonPropertyName("ShopItems")] public List<ShopItemDefinition> ShopItems { get; set; } =
    [
        new() { Key = "shop_xp_small", Name = "Small XP Cache", Description = "Instantly grants 250 XP", RewardType = ShopRewardType.Xp, RewardAmount = 250, CostCredits = 30 },
        new() { Key = "shop_xp_big", Name = "Large XP Cache", Description = "Instantly grants 1000 XP", RewardType = ShopRewardType.Xp, RewardAmount = 1000, CostCredits = 95 },
        new() { Key = "shop_crate_token", Name = "Crate Token", Description = "Adds one XPX crate token", RewardType = ShopRewardType.CrateToken, RewardAmount = 1, CostCredits = 45 },
        new() { Key = "shop_credits_bundle", Name = "Credit Bundle", Description = "Instantly grants 25 credits", RewardType = ShopRewardType.Credits, RewardAmount = 25, CostCredits = 40 }
    ];

    [JsonPropertyName("Crates")] public List<CrateDefinition> Crates { get; set; } =
    [
        new()
        {
            Key = "xpx_case",
            Name = "XPX Case",
            Description = "A weighted reward crate with XP and credit drops.",
            CostCredits = 50,
            Rewards =
            [
                new() { Label = "25 Credits", RewardType = ShopRewardType.Credits, RewardAmount = 25, Weight = 32 },
                new() { Label = "60 Credits", RewardType = ShopRewardType.Credits, RewardAmount = 60, Weight = 18 },
                new() { Label = "150 XP", RewardType = ShopRewardType.Xp, RewardAmount = 150, Weight = 24 },
                new() { Label = "400 XP", RewardType = ShopRewardType.Xp, RewardAmount = 400, Weight = 14 },
                new() { Label = "1 Crate Token", RewardType = ShopRewardType.CrateToken, RewardAmount = 1, Weight = 8 },
                new() { Label = "100 Credits", RewardType = ShopRewardType.Credits, RewardAmount = 100, Weight = 4 }
            ]
        }
    ];

    [JsonPropertyName("MapPool")] public List<string> MapPool { get; set; } =
    [
        "ar_baggage",
        "ar_pool_day",
        "ar_shoots",
        "ar_shoots_night",
        "cs_italy",
        "cs_office",
        "de_ancient",
        "de_anubis",
        "de_dust2",
        "de_inferno",
        "de_mirage",
        "de_nuke",
        "de_overpass",
        "de_train",
        "de_vertigo"
    ];

    [JsonPropertyName("WorkshopMaps")] public List<WorkshopMapOption> WorkshopMaps { get; set; } =
    [
        new() { Id = "3354923062", Label = "Manor" },
        new() { Id = "3070941760", Label = "aim_shaft" },
        new() { Id = "3242420753", Label = "[ARENA 1vs1] am_anubis_p" },
        new() { Id = "3339983232", Label = "Agency" },
        new() { Id = "3395240479", Label = "AIM Map 2" },
        new() { Id = "3329258290", Label = "Basalt" },
        new() { Id = "3075706807", Label = "Biome" },
        new() { Id = "3454386068", Label = "Bonn WIP (Wingman)" },
        new() { Id = "3467065969", Label = "Contact" },
        new() { Id = "3414036782", Label = "Dogtown" },
        new() { Id = "3408790618", Label = "El Dorado" },
        new() { Id = "3219506727", Label = "Lake" },
        new() { Id = "3507728279", Label = "Lublin" },
        new() { Id = "3070560242", Label = "Lunacy" },
        new() { Id = "3433040330", Label = "Wrecked" },
        new() { Id = "3447707473", Label = "Dust 2 Night" },
        new() { Id = "3249860053", Label = "Palacio" },
        new() { Id = "3536622725", Label = "Rooftop" },
        new() { Id = "3531149465", Label = "Echolab" },
        new() { Id = "3542662073", Label = "Transit" },
        new() { Id = "3552466076", Label = "Mocha" },
        new() { Id = "3596198331", Label = "AIM Halloween 1v1" },
        new() { Id = "3643838992", Label = "Matinee" },
        new() { Id = "3663186989", Label = "Boulder" },
        new() { Id = "3685742137", Label = "Dynamic 1v1 - Dust2" },
        new() { Id = "3689913704", Label = "1v1 / 2v2 Aim Training" },
        new() { Id = "3691464498", Label = "Cati" }
    ];

    [JsonPropertyName("AdminXpAmounts")] public List<int> AdminXpAmounts { get; set; } = [50, 100, 250, 500, 1000, 2500, 5000];

    [JsonPropertyName("GameModes")] public List<GameModeOption> GameModes { get; set; } =
    [
        new() { Label = "Casual", Alias = "casual" },
        new() { Label = "Competitive", Alias = "competitive" },
        new() { Label = "Deathmatch", Alias = "deathmatch" },
        new() { Label = "Arms Race", Alias = "armsrace" }
    ];

    [JsonPropertyName("Rewards")] public List<LevelReward> Rewards { get; set; } =
    [
        new() { Level = 5, Tag = "[SPARK]" },
        new() { Level = 15, Tag = "[COMET]" },
        new() { Level = 30, Tag = "[NOVA]" },
        new() { Level = 50, Tag = "[XPX]" },
        new() { Level = 75, KnifeItem = "weapon_knife_flip" },
        new() { Level = 100, Tag = "[NEBULA]" },
        new() { Level = 150, Tag = "[STARFORGED]" },
        new() { Level = 225, Tag = "[CELESTIAL]", KnifeItem = "weapon_knife_bayonet" },
        new() { Level = 350, Tag = "[VOIDWALKER]", KnifeItem = "weapon_knife_karambit" },
        new() { Level = 500, Tag = "[SOLARIS]", KnifeItem = "weapon_knife_butterfly" }
    ];
}

public sealed class KillstreakBonusOption
{
    [JsonPropertyName("Threshold")] public int Threshold { get; set; }
    [JsonPropertyName("RewardXp")] public int RewardXp { get; set; }
    [JsonPropertyName("RewardCredits")] public int RewardCredits { get; set; }
    [JsonPropertyName("Label")] public string Label { get; set; } = string.Empty;
}

public sealed class MultikillBonusOption
{
    [JsonPropertyName("Kills")] public int Kills { get; set; }
    [JsonPropertyName("RewardXp")] public int RewardXp { get; set; }
    [JsonPropertyName("RewardCredits")] public int RewardCredits { get; set; }
    [JsonPropertyName("Label")] public string Label { get; set; } = string.Empty;
}

public sealed class GameModeOption
{
    [JsonPropertyName("Label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("Alias")] public string Alias { get; set; } = string.Empty;
}

public sealed class LevelReward
{
    [JsonPropertyName("Level")] public int Level { get; set; }
    [JsonPropertyName("Tag")] public string Tag { get; set; } = string.Empty;
    [JsonPropertyName("KnifeItem")] public string KnifeItem { get; set; } = string.Empty;
}

public sealed class WorkshopMapOption
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("Label")] public string Label { get; set; } = string.Empty;
}
