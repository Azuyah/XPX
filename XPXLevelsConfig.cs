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
    [JsonPropertyName("BotXpMultiplier")] public double BotXpMultiplier { get; set; } = 0.30d;
    [JsonPropertyName("ShowKillXpMessages")] public bool ShowKillXpMessages { get; set; } = true;
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
        "{LightBlue}Use {White}!level {LightBlue}| {White}!rank {LightBlue}| {White}!top {LightBlue}| {White}!rtv {LightBlue}| {White}!help {LightBlue}| {White}!me",
        "{Yellow}You are level {White}{LEVEL}{Yellow} with {White}{TOTAL_XP}{Yellow} XP. Next unlock: {White}{NEXT_REWARD}{Yellow}."
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
