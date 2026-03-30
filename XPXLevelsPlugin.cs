using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using GameTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace XPXLevels;

[MinimumApiVersion(80)]
public sealed partial class XPXLevelsPlugin : BasePlugin, IPluginConfig<XPXLevelsConfig>
{
    private enum ForcedLoadoutType
    {
        Rifle,
        Pistol,
        Knife
    }

    private const int DefaultBotCount = 10;
    private const float TransientPanelDurationSeconds = 6.0f;
    private const int TransitionSnapshotLifetimeMinutes = 10;
    private const string PermissionRoot = "@XPX/root";
    private const string PermissionMenu = "@XPX/menu";
    private const string PermissionXp = "@XPX/xp";
    private const string PermissionMap = "@XPX/map";
    private const string PermissionKick = "@XPX/kick";
    private const string PermissionVote = "@XPX/vote";

    private static readonly Regex ColorTokenRegex = new(@"\{[A-Za-z]+\}", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ColorTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{Default}"] = ChatColors.Default.ToString(),
        ["{White}"] = ChatColors.White.ToString(),
        ["{DarkRed}"] = ChatColors.DarkRed.ToString(),
        ["{Green}"] = ChatColors.Green.ToString(),
        ["{LightYellow}"] = ChatColors.LightYellow.ToString(),
        ["{LightBlue}"] = ChatColors.LightBlue.ToString(),
        ["{Olive}"] = ChatColors.Olive.ToString(),
        ["{Lime}"] = ChatColors.Lime.ToString(),
        ["{Red}"] = ChatColors.Red.ToString(),
        ["{LightPurple}"] = ChatColors.LightPurple.ToString(),
        ["{Purple}"] = ChatColors.Purple.ToString(),
        ["{Grey}"] = ChatColors.Grey.ToString(),
        ["{Yellow}"] = ChatColors.Yellow.ToString(),
        ["{Gold}"] = ChatColors.Gold.ToString(),
        ["{Silver}"] = ChatColors.Silver.ToString(),
        ["{Blue}"] = ChatColors.Blue.ToString(),
        ["{DarkBlue}"] = ChatColors.DarkBlue.ToString(),
        ["{BlueGrey}"] = ChatColors.BlueGrey.ToString(),
        ["{Magenta}"] = ChatColors.Magenta.ToString(),
        ["{LightRed}"] = ChatColors.LightRed.ToString(),
        ["{Orange}"] = ChatColors.Orange.ToString()
    };

    private readonly Dictionary<ulong, PlayerProgress> _players = new();
    private readonly Dictionary<int, ulong> _slotToSteamId = new();
    private readonly Dictionary<ulong, GameTimer> _helpTimers = new();
    private readonly Dictionary<ulong, GameTimer> _helpRefreshTimers = new();
    private readonly Dictionary<ulong, CounterStrikeSharp.API.Core.Listeners.OnTick> _helpTickHandlers = new();
    private readonly Dictionary<ulong, DateTimeOffset> _lastHelpToggleAt = new();
    private readonly Dictionary<ulong, GameTimer> _levelUpCloseTimers = new();
    private readonly Dictionary<ulong, GameTimer> _levelUpRefreshTimers = new();
    private readonly Dictionary<ulong, CounterStrikeSharp.API.Core.Listeners.OnTick> _levelUpTickHandlers = new();
    private readonly HashSet<ulong> _rtvVotes = new();
    private readonly HashSet<ulong> _openHelpPanels = new();
    private readonly Dictionary<ulong, string> _levelUpHtml = new();
    private readonly Random _random = new();
    private readonly LevelCurve _levelCurve = new();
    private readonly Dictionary<ulong, TransitionSnapshotEntry> _transitionSnapshotBySteamId = new();

    private XPXLevelsRepository? _repository;
    private bool _forcedLoadoutModeEnabled;
    private ForcedLoadoutType _forcedLoadout = ForcedLoadoutType.Rifle;
    private MapVoteSession? _activeMapVote;
    private GameTimer? _activeMapVoteTimer;
    private GameTimer? _activeMapVoteReminderTimer;
    private GameTimer? _autosaveTimer;
    private string? _transitionSnapshotPath;

    public override string ModuleName => "XPX Levels";
    public override string ModuleVersion => "1.4.1";
    public override string ModuleAuthor => "OpenAI";
    public override string ModuleDescription => "Levels, XP rewards, RTV, and admin tools for XPX CS2.";

    public XPXLevelsConfig Config { get; set; } = new();

    public void OnConfigParsed(XPXLevelsConfig config)
    {
        config.MaxLevel = Math.Clamp(config.MaxLevel, 1, 500);
        config.BaseXpToLevel = Math.Max(1, config.BaseXpToLevel);
        config.XpLinearGrowthPerLevel = Math.Max(0d, config.XpLinearGrowthPerLevel);
        config.XpQuadraticGrowthPerLevel = Math.Max(0d, config.XpQuadraticGrowthPerLevel);
        config.CasualCompetitiveKillXp = Math.Max(0, config.CasualCompetitiveKillXp);
        config.FastModeKillXp = Math.Max(0, config.FastModeKillXp);
        config.KnifeKillBonusXp = Math.Max(0, config.KnifeKillBonusXp);
        config.HeadshotBonusXp = Math.Max(0, config.HeadshotBonusXp);
        config.RoundWinXp = Math.Max(0, config.RoundWinXp);
        config.BombPlantXp = Math.Max(0, config.BombPlantXp);
        config.BombDefuseXp = Math.Max(0, config.BombDefuseXp);
        config.AssistXp = Math.Max(0, config.AssistXp);
        config.MvpXp = Math.Max(0, config.MvpXp);
        config.ClutchXp = Math.Max(0, config.ClutchXp);
        config.FirstBloodXp = Math.Max(0, config.FirstBloodXp);
        config.CasualCompetitiveKillCredits = Math.Max(0, config.CasualCompetitiveKillCredits);
        config.FastModeKillCredits = Math.Max(0, config.FastModeKillCredits);
        config.RoundWinCredits = Math.Max(0, config.RoundWinCredits);
        config.AssistCredits = Math.Max(0, config.AssistCredits);
        config.MvpCredits = Math.Max(0, config.MvpCredits);
        config.FirstBloodCredits = Math.Max(0, config.FirstBloodCredits);
        config.ClutchCredits = Math.Max(0, config.ClutchCredits);
        config.ChickenKillXp = Math.Max(0, config.ChickenKillXp);
        config.BotXpMultiplier = Math.Clamp(config.BotXpMultiplier, 0d, 1d);
        config.GambleWinChancePercent = Math.Clamp(config.GambleWinChancePercent, 1, 99);
        config.GambleMinXp = Math.Max(1, config.GambleMinXp);
        config.GambleMaxXp = Math.Max(config.GambleMinXp, config.GambleMaxXp);
        config.GambleCooldownSeconds = Math.Max(0, config.GambleCooldownSeconds);
        config.RtvRequiredRatio = Math.Clamp(config.RtvRequiredRatio, 0.10d, 1.0d);
        config.RtvVoteDurationSeconds = Math.Max(10, config.RtvVoteDurationSeconds);
        config.RtvReminderSeconds = Math.Clamp(config.RtvReminderSeconds, 0, Math.Max(0, config.RtvVoteDurationSeconds - 1));
        config.RtvMapOptionCount = Math.Clamp(config.RtvMapOptionCount, 2, 8);
        config.MapChangeDelaySeconds = Math.Max(1, config.MapChangeDelaySeconds);
        config.TopCount = Math.Clamp(config.TopCount, 3, 15);
        config.ChatPrefix = string.IsNullOrWhiteSpace(config.ChatPrefix) ? "{Green}[XPX]{Default}" : config.ChatPrefix.Trim();
        config.ServerName = string.IsNullOrWhiteSpace(config.ServerName) ? "XPX CS2" : config.ServerName.Trim();
        config.CurrencyName = string.IsNullOrWhiteSpace(config.CurrencyName) ? "Credits" : config.CurrencyName.Trim();
        config.KickReason = string.IsNullOrWhiteSpace(config.KickReason) ? "Removed by an XPX admin." : config.KickReason.Trim();
        config.WelcomeMessages = config.WelcomeMessages.Where(static message => !string.IsNullOrWhiteSpace(message)).ToList();
        config.MapPool = config.MapPool.Where(static map => !string.IsNullOrWhiteSpace(map))
            .Select(static map => map.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.WorkshopMaps = config.WorkshopMaps
            .Where(workshopMap => !string.IsNullOrWhiteSpace(workshopMap.Id) && !string.IsNullOrWhiteSpace(workshopMap.Label))
            .Select(workshopMap => new WorkshopMapOption
            {
                Id = workshopMap.Id.Trim(),
                Label = workshopMap.Label.Trim()
            })
            .Where(workshopMap => workshopMap.Id.All(char.IsDigit))
            .GroupBy(workshopMap => workshopMap.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(workshopMap => workshopMap.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.AdminXpAmounts = config.AdminXpAmounts.Where(static amount => amount > 0)
            .Distinct()
            .OrderBy(static amount => amount)
            .ToList();
        config.GameModes = config.GameModes.Where(static mode => !string.IsNullOrWhiteSpace(mode.Label) && !string.IsNullOrWhiteSpace(mode.Alias))
            .ToList();
        config.Rewards = config.Rewards.Where(reward => reward.Level > 0 && reward.Level <= config.MaxLevel)
            .OrderBy(reward => reward.Level)
            .ToList();
        config.KillstreakBonuses = config.KillstreakBonuses
            .Where(static bonus => bonus.Threshold > 1)
            .OrderBy(static bonus => bonus.Threshold)
            .ToList();
        config.MultikillBonuses = config.MultikillBonuses
            .Where(static bonus => bonus.Kills > 1)
            .OrderBy(static bonus => bonus.Kills)
            .ToList();
        config.DailyMissions = config.DailyMissions
            .Where(static mission => !string.IsNullOrWhiteSpace(mission.Key) && mission.Goal > 0)
            .ToList();
        config.WeeklyMissions = config.WeeklyMissions
            .Where(static mission => !string.IsNullOrWhiteSpace(mission.Key) && mission.Goal > 0)
            .ToList();
        config.Achievements = config.Achievements
            .Where(static achievement => !string.IsNullOrWhiteSpace(achievement.Key) && achievement.Goal > 0)
            .ToList();
        config.ShopItems = config.ShopItems
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key) && item.CostCredits >= 0)
            .ToList();
        config.Crates = config.Crates
            .Where(crate => !string.IsNullOrWhiteSpace(crate.Key) && crate.Rewards.Count > 0)
            .ToList();

        Config = config;
        _levelCurve.Rebuild(Config);
    }

    public override void Load(bool hotReload)
    {
        _repository = new XPXLevelsRepository(ModuleDirectory);
        _repository.Initialize();
        InitializeTransitionSnapshot();

        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        AddCommandListener(null, OnAnyCommandPre, HookMode.Pre);
        _autosaveTimer = AddTimer(60.0f, SaveAllPlayerProgress, TimerFlags.REPEAT);

        ResetRtvState();
        ScheduleOnlinePlayerSyncs();
    }

    public override void Unload(bool hotReload)
    {
        _autosaveTimer?.Kill();
        _activeMapVoteTimer?.Kill();
        _activeMapVoteReminderTimer?.Kill();
        foreach (var timer in _helpRefreshTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _helpTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _levelUpRefreshTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _levelUpCloseTimers.Values)
        {
            timer.Kill();
        }

        if (_repository is null)
        {
            return;
        }

        SaveAllPlayerProgress();
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        if (@event.Bot || @event.Xuid <= 0 || !IsRealPlayer(@event.Userid))
        {
            return HookResult.Continue;
        }

        var player = @event.Userid!;
        _slotToSteamId[player.Slot] = @event.Xuid;
        EnsurePlayerProgress(player, reloadFromRepository: true);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerInfo(EventPlayerInfo @event, GameEventInfo info)
    {
        if (@event.Bot || @event.Steamid <= 0 || !IsRealPlayer(@event.Userid))
        {
            return HookResult.Continue;
        }

        var player = @event.Userid!;
        _slotToSteamId[player.Slot] = @event.Steamid;
        EnsurePlayerProgress(player, reloadFromRepository: true);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!IsRealPlayer(player))
        {
            return HookResult.Continue;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return HookResult.Continue;
        }

        AddTimer(0.25f, () =>
        {
            if (player is not null && IsRealPlayer(player))
            {
                ApplyRewardState(player, progress);
                ApplySpecialLoadout(player);
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (IsWarmupActive())
        {
            return HookResult.Continue;
        }

        var attacker = @event.Attacker;
        var victim = @event.Userid;
        if (!IsRealPlayer(attacker) || !IsXpEligibleTarget(victim))
        {
            return HookResult.Continue;
        }

        var killer = attacker!;
        var deadPlayer = victim!;
        if (IsChickenTarget(deadPlayer))
        {
            AdjustXp(killer, Config.ChickenKillXp, "killing a chicken", true);
            return HookResult.Continue;
        }

        if (!CanAwardKillXp(killer, deadPlayer))
        {
            return HookResult.Continue;
        }

        var knifeKill = IsKnifeKill(@event.Weapon);
        var (xpToAward, reason) = GetKillXpAward(deadPlayer, knifeKill, @event.Headshot);
        if (xpToAward <= 0)
        {
            return HookResult.Continue;
        }

        AdjustXp(killer, xpToAward, reason, Config.ShowKillXpMessages);
        HandleKillFeatureProgress(killer, deadPlayer, @event.Weapon, @event.Headshot, knifeKill);
        if (IsRealPlayer(@event.Assister) && @event.Assister != attacker && @event.Assister != victim)
        {
            HandleAssistFeatureProgress(@event.Assister!);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        if (IsWarmupActive() || !IsRealPlayer(@event.Userid))
        {
            return HookResult.Continue;
        }

        AdjustXp(@event.Userid!, Config.BombPlantXp, "bomb plant", true);
        HandleBombFeatureProgress(@event.Userid!, MissionObjective.BombPlants);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        if (IsWarmupActive() || !IsRealPlayer(@event.Userid))
        {
            return HookResult.Continue;
        }

        AdjustXp(@event.Userid!, Config.BombDefuseXp, "bomb defuse", true);
        HandleBombFeatureProgress(@event.Userid!, MissionObjective.BombDefuses);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (IsWarmupActive() || Config.RoundWinXp <= 0)
        {
            return HookResult.Continue;
        }

        if (@event.Winner is not 2 and not 3)
        {
            return HookResult.Continue;
        }

        var winningTeam = (CsTeam)@event.Winner;
        foreach (var player in GetHumanPlayers().Where(player => player.Team == winningTeam))
        {
            AdjustXp(player, Config.RoundWinXp, "round win", true);
        }

        HandleRoundWinFeatureProgress(winningTeam);

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var player = Utilities.GetPlayerFromUserid(@event.Userid);
        if (!IsRealPlayer(player))
        {
            return HookResult.Continue;
        }

        var text = @event.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('!') || text.StartsWith('/'))
        {
            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    [ConsoleCommand("css_level", "Show your current XPX level")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnLevelCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }
        ShowLevelOverview(player!);
    }

    [ConsoleCommand("css_rank", "Show your current XPX rank")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnRankCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        ShowRankOverview(player!);
    }

    [ConsoleCommand("css_top", "Show the XPX leaderboard")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnTopCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }
        ShowTopOverview(player!);
    }

    [ConsoleCommand("css_help", "Show XPX help and progression info")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        ToggleHelpPanel(player!);
    }

    [ConsoleCommand("css_me", "Open your XPX quick menu")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnMeCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        OpenMeMenu(player!);
    }

    [ConsoleCommand("css_gamble", "Gamble a chunk of your XP")]
    [CommandHelper(minArgs: 1, usage: "[xp]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnGambleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        if (!int.TryParse(command.GetArg(1), out var requestedXp))
        {
            Reply(command, "{Red}Usage: {White}!gamble <xp>");
            return;
        }

        requestedXp = Math.Clamp(requestedXp, Config.GambleMinXp, Config.GambleMaxXp);
        if (progress.TotalXp < requestedXp)
        {
            Reply(command, "{Red}You do not have enough XP to gamble that amount.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var nextAllowed = progress.LastGambleAttemptUtc.AddSeconds(Config.GambleCooldownSeconds);
        if (Config.GambleCooldownSeconds > 0 && now < nextAllowed)
        {
            var secondsLeft = Math.Max(1, (int)Math.Ceiling((nextAllowed - now).TotalSeconds));
            Reply(command, "{Red}You need to wait {White}" + secondsLeft + "{Red}s before gambling again.");
            return;
        }

        progress.LastGambleAttemptUtc = now;
        var gambler = player!;
        var won = _random.Next(1, 101) <= Config.GambleWinChancePercent;
        var delta = won ? requestedXp : -requestedXp;
        AdjustXp(gambler, delta, "gamble", false);
        Reply(command,
            won
                ? "{Green}Lucky hit. You won {White}" + requestedXp.ToString("N0", CultureInfo.InvariantCulture) + "{Green} XP."
                : "{Red}Bad beat. You lost {White}" + requestedXp.ToString("N0", CultureInfo.InvariantCulture) + "{Red} XP.");
    }

    [ConsoleCommand("css_rtv", "Rock the vote for a map change")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnRtvCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player) || !TryGetSteamId(player, out var steamId))
        {
            return;
        }

        SubmitRtvVote(player!, steamId);
    }

    [ConsoleCommand("css_vote", "Re-open the active XPX map vote")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnVoteCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        if (_activeMapVote is null)
        {
            Reply(command, "{Yellow}There is no active map vote right now.");
            return;
        }

        OpenMapVoteMenu(player!);
    }

    [ConsoleCommand("css_admin", "Open the XPX admin menu")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [RequiresPermissions(PermissionMenu)]
    public void OnAdminCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        OpenAdminMenu(player!);
    }

    [ConsoleCommand("css_bindmenu", "Bind 1-9 to weapon slots and XPX menu input")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnBindMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        ApplyPersistentMenuKeyBinds(player!);
        Reply(player!, "{Green}XPX menu binds were applied with the server-side bind command.");
        Reply(player!, "{Silver}Your {White}1-9{Silver} keys should now use weapon slots and also drive XPX menus.");
        Reply(player!, "{Silver}If CS2 does not pick them up in the current session, restart CS2 once and they should load from your saved config.");
    }

    [ConsoleCommand("css_givexp", "Give XP to a player or target group")]
    [RequiresPermissions(PermissionXp)]
    [CommandHelper(minArgs: 2, usage: "[target] [amount]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnGiveXpCommand(CCSPlayerController? caller, CommandInfo command)
    {
        HandleAdminXpCommand(caller, command, true);
    }

    [ConsoleCommand("css_removexp", "Remove XP from a player or target group")]
    [RequiresPermissions(PermissionXp)]
    [CommandHelper(minArgs: 2, usage: "[target] [amount]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRemoveXpCommand(CCSPlayerController? caller, CommandInfo command)
    {
        HandleAdminXpCommand(caller, command, false);
    }

    [ConsoleCommand("css_changemap", "Change to a specific map")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(minArgs: 1, usage: "[map]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnChangeMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        ChangeMapTo(command.GetArg(1), caller?.PlayerName ?? "Console", command);
    }

    [ConsoleCommand("css_restartmap", "Restart the current map")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnRestartMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        RestartCurrentMap(caller?.PlayerName ?? "Console");
        Reply(command, "{Gold}Restarting the current map.");
    }

    [ConsoleCommand("css_setmode", "Set the current game mode alias")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(minArgs: 1, usage: "[alias]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSetModeCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryGetGameMode(command.GetArg(1), out var gameMode))
        {
            Reply(command, "{Red}Unknown mode. Valid aliases: {White}" + string.Join(", ", Config.GameModes.Select(mode => mode.Alias)));
            return;
        }

        ApplyGameMode(gameMode, caller?.PlayerName ?? "Console");
        Reply(command, "{Gold}Switching to {White}" + gameMode.Label + "{Gold}.");
    }

    [ConsoleCommand("css_kick", "Kick a player")]
    [RequiresPermissions(PermissionKick)]
    [CommandHelper(minArgs: 1, usage: "[target]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnKickCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var targets = command.GetArgTargetResult(1).Players.Where(IsRealPlayer).ToList();
        if (targets.Count == 0)
        {
            Reply(command, "{Red}No valid targets matched.");
            return;
        }

        foreach (var target in targets)
        {
            if (caller is not null && !AdminManager.CanPlayerTarget(caller, target))
            {
                continue;
            }

            KickPlayer(target, caller?.PlayerName ?? "Console");
        }

        Reply(command, "{Gold}Kick command processed.");
    }

    [ConsoleCommand("css_kickbots", "Kick all bots and set bot quota to 0")]
    [RequiresPermissions(PermissionKick)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnKickBotsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        KickAllBots(caller?.PlayerName ?? "Console", command, caller);
    }

    [ConsoleCommand("css_addbots", "Add bots back to the server")]
    [RequiresPermissions(PermissionKick)]
    [CommandHelper(usage: "[count]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnAddBotsCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var desiredBots = DefaultBotCount;
        if (command.ArgCount > 1)
        {
            if (!int.TryParse(command.GetArg(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out desiredBots))
            {
                Reply(command, "{Red}Invalid bot count. Use a whole number like {White}!addbots 6");
                return;
            }
        }

        AddBots(caller?.PlayerName ?? "Console", desiredBots, command, caller);
    }

    [ConsoleCommand("css_forceloadout", "Force a global rifle, pistol, or knife loadout for all players")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(minArgs: 1, usage: "[rifle|pistol|knife|off]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnForceLoadoutCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!TryParseForcedLoadout(command.GetArg(1), out var loadout, out var disable))
        {
            Reply(command, "{Red}Unknown loadout. Use {White}rifle{Red}, {White}pistol{Red}, {White}knife{Red}, or {White}off");
            return;
        }

        if (disable)
        {
            DisableForcedLoadoutMode(caller?.PlayerName ?? "Console", command, caller);
            return;
        }

        EnableForcedLoadoutMode(loadout, caller?.PlayerName ?? "Console", command, caller);
    }

    [ConsoleCommand("css_forcevote", "Start a map vote immediately")]
    [RequiresPermissions(PermissionVote)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnForceVoteCommand(CCSPlayerController? caller, CommandInfo command)
    {
        StartMapVote(caller, true);
        Reply(command, "{Gold}Force vote command processed.");
    }

    [ConsoleCommand("css_cancelvote", "Cancel the active map vote")]
    [RequiresPermissions(PermissionVote)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnCancelVoteCommand(CCSPlayerController? caller, CommandInfo command)
    {
        CancelActiveVote(caller?.PlayerName ?? "Console", command);
    }

    private void OnMapStart(string mapName)
    {
        ResetRtvState();
        ResetTransientUiState();
        ResetFeatureRoundState();
        ScheduleOnlinePlayerSyncs();
        if (_forcedLoadoutModeEnabled)
        {
            AddTimer(1.0f, () =>
            {
                ApplyDeathmatchModeCommands();
                ApplyForcedLoadoutServerRules();
                ReapplyForcedLoadoutToAlivePlayers();
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        AddTimer(90.0f, ClearTransitionSnapshot, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void OnClientAuthorized(int playerSlot, SteamID steamId)
    {
        _slotToSteamId[playerSlot] = steamId.SteamId64;
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (IsRealPlayer(player))
        {
            EnsurePlayerProgress(player, reloadFromRepository: true);
        }
    }

    private void OnClientPutInServer(int playerSlot)
    {
        AddTimer(3.0f, () =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (!IsRealPlayer(player))
            {
                return;
            }

            var progress = EnsurePlayerProgress(player, reloadFromRepository: true);
            if (progress is null)
            {
                return;
            }

            var joinedPlayer = player!;
            EnsureFeatureStateLoaded(joinedPlayer);
            ApplyRewardState(joinedPlayer, progress);
            foreach (var message in Config.WelcomeMessages)
            {
                Reply(joinedPlayer, ExpandTokens(joinedPlayer, progress, message));
            }

            if (TryGetSteamId(joinedPlayer, out var steamId))
            {
                AddTimer(1.2f, () =>
                {
                    if (!IsRealPlayer(joinedPlayer) || _openHelpPanels.Contains(steamId) || MenuManager.GetActiveMenu(joinedPlayer) is not null)
                    {
                        return;
                    }

                    ShowHelpPanel(joinedPlayer, steamId, closeActiveMenu: false);
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void OnClientDisconnect(int playerSlot)
    {
        if (!_slotToSteamId.Remove(playerSlot, out var steamId))
        {
            return;
        }

        CloseHelpPanel(steamId);
        StopLevelUpPanel(steamId, false);
        _lastHelpToggleAt.Remove(steamId);
        _rtvVotes.Remove(steamId);
        _activeMapVote?.VotesBySteamId.Remove(steamId);

        if (_repository is not null && _players.Remove(steamId, out var progress))
        {
            PersistProgressSafely(progress);
        }

        UnloadFeatureStateForPlayer(steamId);
    }

    private HookResult OnAnyCommandPre(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player) && TryHandleMenuChatSelection(player!, command))
        {
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private void SaveAllPlayerProgress()
    {
        if (_repository is null)
        {
            return;
        }

        foreach (var progress in _players.Values)
        {
            PersistProgressSafely(progress);
        }

        SaveAllFeatureState();
        SaveTransitionSnapshot();
    }

    private void PersistProgressSafely(PlayerProgress progress)
    {
        if (_repository is null)
        {
            return;
        }

        progress.PlayerName = NormalizeStoredPlayerName(progress.PlayerName);
        var storedProgress = _repository.GetPlayer(progress.SteamId);
        if (storedProgress is null)
        {
            _repository.SavePlayer(progress);
            return;
        }

        if (progress.TotalXp > storedProgress.TotalXp)
        {
            _repository.SavePlayer(progress);
            return;
        }

        if (progress.Credits != storedProgress.Credits || progress.CrateTokens != storedProgress.CrateTokens)
        {
            _repository.SavePlayer(progress);
            return;
        }

        if (progress.TotalXp < storedProgress.TotalXp)
        {
            Logger.LogWarning("XPX skipped stale backup save for {SteamId}: in-memory {CurrentXp} XP is lower than stored {StoredXp} XP", progress.SteamId, progress.TotalXp, storedProgress.TotalXp);
            return;
        }

        if (progress.TotalXp == storedProgress.TotalXp &&
            !string.Equals(progress.PlayerName, storedProgress.PlayerName, StringComparison.Ordinal))
        {
            _repository.SavePlayer(progress);
        }
    }

    private void SyncOnlinePlayers()
    {
        foreach (var player in GetHumanPlayers())
        {
            var progress = EnsurePlayerProgress(player, reloadFromRepository: true);
            if (progress is not null)
            {
                ApplyRewardState(player, progress, refreshEquipment: false);
            }
        }
    }

    private void InitializeTransitionSnapshot()
    {
        var dataDirectory = Path.Combine(Application.RootDirectory, "data", "XPXLevels");
        Directory.CreateDirectory(dataDirectory);
        _transitionSnapshotPath = Path.Combine(dataDirectory, "transition-snapshot.json");
        LoadTransitionSnapshot();
    }

    private void LoadTransitionSnapshot()
    {
        _transitionSnapshotBySteamId.Clear();
        if (string.IsNullOrWhiteSpace(_transitionSnapshotPath) || !File.Exists(_transitionSnapshotPath))
        {
            return;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<TransitionSnapshot>(File.ReadAllText(_transitionSnapshotPath));
            if (snapshot is null)
            {
                return;
            }

            if (DateTimeOffset.UtcNow - snapshot.CreatedUtc > TimeSpan.FromMinutes(TransitionSnapshotLifetimeMinutes))
            {
                ClearTransitionSnapshot();
                return;
            }

            foreach (var entry in snapshot.Players.Where(static entry => entry.SteamId > 0))
            {
                _transitionSnapshotBySteamId[entry.SteamId] = entry;
            }
        }
        catch
        {
        }
    }

    private void SaveTransitionSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_transitionSnapshotPath))
        {
            return;
        }

        try
        {
            var snapshot = new TransitionSnapshot
            {
                CreatedUtc = DateTimeOffset.UtcNow,
                Players = _players.Values
                    .Select(progress => new TransitionSnapshotEntry
                    {
                        SteamId = progress.SteamId,
                        PlayerName = NormalizeStoredPlayerName(progress.PlayerName),
                        TotalXp = progress.TotalXp
                    })
                    .ToList()
            };

            foreach (var entry in snapshot.Players)
            {
                _transitionSnapshotBySteamId[entry.SteamId] = entry;
            }

            File.WriteAllText(_transitionSnapshotPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
        }
    }

    private void ClearTransitionSnapshot()
    {
        _transitionSnapshotBySteamId.Clear();
        if (string.IsNullOrWhiteSpace(_transitionSnapshotPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_transitionSnapshotPath))
            {
                File.Delete(_transitionSnapshotPath);
            }
        }
        catch
        {
        }
    }

    private void ScheduleOnlinePlayerSyncs()
    {
        foreach (var delay in new[] { 1.0f, 3.0f, 6.0f, 10.0f, 15.0f })
        {
            AddTimer(delay, SyncOnlinePlayers, TimerFlags.STOP_ON_MAPCHANGE);
        }
    }

    private void HandleAdminXpCommand(CCSPlayerController? caller, CommandInfo command, bool add)
    {
        if (!int.TryParse(command.GetArg(2), out var amount) || amount <= 0)
        {
            Reply(command, "{Red}XP amount must be a positive number.");
            return;
        }

        var delta = add ? amount : -amount;
        var targets = command.GetArgTargetResult(1).Players.Where(IsRealPlayer).ToList();
        if (targets.Count == 0)
        {
            Reply(command, "{Red}No valid targets matched.");
            return;
        }

        foreach (var target in targets)
        {
            if (caller is not null && !AdminManager.CanPlayerTarget(caller, target))
            {
                continue;
            }

            AdjustXp(target, delta, add ? "admin grant" : "admin removal", false);
            Reply(target,
                add
                    ? "{Green}An admin granted you {White}" + amount.ToString("N0", CultureInfo.InvariantCulture) + "{Green} XP."
                    : "{Red}An admin removed {White}" + amount.ToString("N0", CultureInfo.InvariantCulture) + "{Red} XP.");
        }

        Reply(command,
            (add ? "{Green}Granted {White}" : "{Red}Removed {White}") +
            amount.ToString("N0", CultureInfo.InvariantCulture) +
            (add ? "{Green} XP for matched targets." : "{Red} XP for matched targets."));
    }

    private void OpenAdminMenu(CCSPlayerController player)
    {
        var menu = CreateMenu("XPX Admin");

        if (HasPermission(player, PermissionMap))
        {
            menu.AddMenuOption("Change map", (admin, _) => OpenChangeMapMenu(admin));
            menu.AddMenuOption("Restart current map", (admin, _) => RestartCurrentMap(admin.PlayerName));
            menu.AddMenuOption("Change game mode", (admin, _) => OpenGameModeMenu(admin));
            menu.AddMenuOption("Special rounds", (admin, _) => OpenSpecialRoundsMenu(admin));
            menu.AddMenuOption($"Weapon loadout ({GetForcedLoadoutStatusLabel()})", (admin, _) => OpenForcedLoadoutMenu(admin));
        }

        if (HasPermission(player, PermissionKick))
        {
            menu.AddMenuOption("Kick player", (admin, _) => OpenKickMenu(admin));
            menu.AddMenuOption("Kick all bots", (admin, _) => KickAllBots(admin.PlayerName, actor: admin));
            menu.AddMenuOption($"Add {DefaultBotCount} bots", (admin, _) => AddBots(admin.PlayerName, DefaultBotCount, actor: admin));
        }

        if (HasPermission(player, PermissionXp))
        {
            menu.AddMenuOption("Give XP", (admin, _) => OpenXpAmountMenu(admin, true));
            menu.AddMenuOption("Remove XP", (admin, _) => OpenXpAmountMenu(admin, false));
        }

        if (HasPermission(player, PermissionVote))
        {
            menu.AddMenuOption("Start map vote", (admin, _) => StartMapVote(admin, true));
            if (_activeMapVote is not null)
            {
                menu.AddMenuOption("Cancel active vote", (admin, _) => CancelActiveVote(admin.PlayerName));
            }
        }

        OpenXPXMenu(player, menu);
        Reply(player, "{Silver}Use local {White}1-9{Silver} binds if you have them, or type {White}!1{Silver}-{White}!9{Silver} in chat.");
    }

    private void OpenMeMenu(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var state = _levelCurve.GetState(progress.TotalXp);
        var tag = GetCurrentVisibleTag(progress);
        var menu = CreateMenu($"Me | {RenderShortLevelLabel(state.Level)} | {(string.IsNullOrWhiteSpace(tag) ? "NO TAG" : tag)}");

        menu.AddMenuOption("My level overview", (_, _) => OpenLevelOverviewMenu(player));
        menu.AddMenuOption("My rank", (_, _) => OpenRankOverviewMenu(player));
        menu.AddMenuOption("Top players", (_, _) => OpenTopOverviewMenu(player));
        menu.AddMenuOption("Stats", (_, _) => OpenStatsMenu(player));
        menu.AddMenuOption("Missions", (_, _) => OpenMissionsMenu(player));
        menu.AddMenuOption("Achievements", (_, _) => OpenAchievementsMenu(player));
        menu.AddMenuOption("Shop", (_, _) => OpenShopMenu(player));
        menu.AddMenuOption("Crates / wallet", (_, _) => OpenCrateMenu(player));
        menu.AddMenuOption(_activeMapVote is null ? "Rock the vote" : "Vote for next map", (_, _) =>
        {
            if (_activeMapVote is null)
            {
                SubmitRtvVote(player);
            }
            else
            {
                OpenMapVoteMenu(player);
            }
        });
        menu.AddMenuOption("XPX help", (_, _) => ToggleHelpPanel(player));

        if (HasPermission(player, PermissionMenu))
        {
            menu.AddMenuOption("Admin menu", (_, _) => OpenAdminMenu(player));
        }

        OpenXPXMenu(player, menu);
        Reply(player, "{Silver}Use local {White}1-9{Silver} binds if you have them, or type {White}!1{Silver}-{White}!9{Silver} in chat.");
    }

    private void OpenChangeMapMenu(CCSPlayerController player)
    {
        var maps = GetAvailableMaps();
        var menu = CreateMenu("Change Map");
        foreach (var map in maps)
        {
            var selectedMap = map;
            menu.AddMenuOption(selectedMap.DisplayName, (_, _) =>
            {
                ChangeMapTo(selectedMap.Key, player.PlayerName);
            });
        }

        OpenXPXMenu(player, menu);
    }

    private void RestartCurrentMap(string actorName)
    {
        var currentMap = Server.MapName;
        SaveAllPlayerProgress();
        Broadcast("{Gold}" + actorName + "{Default} restarted the current map.");
        AddTimer(Config.MapChangeDelaySeconds, () => Server.ExecuteCommand($"changelevel \"{currentMap}\""), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void OpenGameModeMenu(CCSPlayerController player)
    {
        var menu = CreateMenu("Change Game Mode");
        foreach (var mode in Config.GameModes)
        {
            var selectedMode = mode;
            menu.AddMenuOption(selectedMode.Label, (_, _) =>
            {
                ApplyGameMode(selectedMode, player.PlayerName);
            });
        }

        OpenXPXMenu(player, menu);
    }

    private void OpenForcedLoadoutMenu(CCSPlayerController player)
    {
        var menu = CreateMenu("Weapon Loadout");
        menu.AddMenuOption("Rifles for all players", (admin, _) => EnableForcedLoadoutMode(ForcedLoadoutType.Rifle, admin.PlayerName, actor: admin));
        menu.AddMenuOption("Pistols for all players", (admin, _) => EnableForcedLoadoutMode(ForcedLoadoutType.Pistol, admin.PlayerName, actor: admin));
        menu.AddMenuOption("Knives for all players", (admin, _) => EnableForcedLoadoutMode(ForcedLoadoutType.Knife, admin.PlayerName, actor: admin));
        if (_forcedLoadoutModeEnabled)
        {
            menu.AddMenuOption("Disable forced loadout", (admin, _) => DisableForcedLoadoutMode(admin.PlayerName, actor: admin));
        }

        OpenXPXMenu(player, menu);
    }

    private void OpenKickMenu(CCSPlayerController player)
    {
        var menu = CreateMenu("Kick Player");
        foreach (var target in GetHumanPlayers().Where(target => target != player && AdminManager.CanPlayerTarget(player, target)))
        {
            var selectedTarget = target;
            menu.AddMenuOption(selectedTarget.PlayerName, (_, _) =>
            {
                KickPlayer(selectedTarget, player.PlayerName);
            });
        }

        OpenXPXMenu(player, menu);
    }

    private void OpenXpAmountMenu(CCSPlayerController player, bool add)
    {
        var menu = CreateMenu(add ? "Give XP" : "Remove XP");
        foreach (var amount in Config.AdminXpAmounts)
        {
            var selectedAmount = amount;
            menu.AddMenuOption(selectedAmount.ToString("N0", CultureInfo.InvariantCulture), (_, _) =>
            {
                OpenXpTargetMenu(player, add, selectedAmount);
            });
        }

        OpenXPXMenu(player, menu);
    }

    private void OpenXpTargetMenu(CCSPlayerController player, bool add, int amount)
    {
        var menu = CreateMenu(add ? $"Give {amount:N0} XP" : $"Remove {amount:N0} XP");
        foreach (var target in GetHumanPlayers().Where(target => AdminManager.CanPlayerTarget(player, target)))
        {
            var selectedTarget = target;
            menu.AddMenuOption(selectedTarget.PlayerName, (_, _) =>
            {
                AdjustXp(selectedTarget, add ? amount : -amount, add ? "admin grant" : "admin removal", false);
                Reply(selectedTarget,
                    add
                        ? "{Green}You received {White}" + amount.ToString("N0", CultureInfo.InvariantCulture) + "{Green} XP from an admin."
                        : "{Red}An admin removed {White}" + amount.ToString("N0", CultureInfo.InvariantCulture) + "{Red} XP.");
                Reply(player,
                    (add ? "{Green}Granted {White}" : "{Red}Removed {White}") +
                    amount.ToString("N0", CultureInfo.InvariantCulture) +
                    "{Default} XP " +
                    (add ? "to {White}" : "from {White}") +
                    selectedTarget.PlayerName + "{Default}.");
            });
        }

        OpenXPXMenu(player, menu);
    }

    private void OpenMapVoteMenu(CCSPlayerController player)
    {
        if (_activeMapVote is null || !TryGetSteamId(player, out var steamId))
        {
            return;
        }

        var secondsRemaining = Math.Max(0, (int)Math.Ceiling((_activeMapVote.EndsAtUtc - DateTimeOffset.UtcNow).TotalSeconds));
        var menu = CreateMenu(secondsRemaining > 0 ? $"Map Vote ({secondsRemaining}s)" : "Map Vote");
        foreach (var map in _activeMapVote.Options)
        {
            var selectedMap = map;
            menu.AddMenuOption(selectedMap.DisplayName, (_, _) =>
            {
                if (_activeMapVote is null)
                {
                    return;
                }

                _activeMapVote.VotesBySteamId[steamId] = selectedMap.Key;
                CloseActiveXPXMenu(player);
                Reply(player, "{Green}You voted for {White}" + selectedMap.DisplayName + "{Green}.");
            });
        }

        OpenXPXMenu(player, menu);
        Reply(player, "{Silver}Use local {White}1-9{Silver} binds if you have them, or type {White}!1{Silver}-{White}!9{Silver} in chat.");
    }

    private void StartMapVote(CCSPlayerController? initiator, bool forcedByAdmin)
    {
        if (_activeMapVote is not null)
        {
            if (initiator is not null)
            {
                OpenMapVoteMenu(initiator);
                Reply(initiator, "{Yellow}A map vote is already active.");
            }

            return;
        }

        var options = PickMapVoteOptions();
        if (options.Count < 2)
        {
            if (initiator is not null)
            {
                Reply(initiator, "{Red}Not enough valid maps were found for a vote.");
            }

            return;
        }

        ResetRtvState();
        _activeMapVote = new MapVoteSession(options, initiator?.PlayerName ?? "RTV", DateTimeOffset.UtcNow.AddSeconds(Config.RtvVoteDurationSeconds));
        _activeMapVoteTimer = AddTimer(Config.RtvVoteDurationSeconds, FinalizeMapVote, TimerFlags.STOP_ON_MAPCHANGE);
        if (Config.RtvReminderSeconds > 0 && Config.RtvReminderSeconds < Config.RtvVoteDurationSeconds)
        {
            _activeMapVoteReminderTimer = AddTimer(Config.RtvVoteDurationSeconds - Config.RtvReminderSeconds, BroadcastVoteReminder, TimerFlags.STOP_ON_MAPCHANGE);
        }

        if (forcedByAdmin && initiator is not null)
        {
            Broadcast("{Gold}" + initiator.PlayerName + "{Default} started a map vote. Use the menu to vote.");
        }
        else
        {
            Broadcast("{Gold}RTV threshold reached. Use the menu to vote for the next map.");
        }

        Broadcast("{Silver}Vote options: {White}" + string.Join("{Default}, {White}", options.Select(option => option.DisplayName)));

        foreach (var target in GetHumanPlayers())
        {
            OpenMapVoteMenu(target);
        }
    }

    private void FinalizeMapVote()
    {
        if (_activeMapVote is null)
        {
            return;
        }

        var session = _activeMapVote;
        _activeMapVote = null;
        _activeMapVoteTimer = null;
        _activeMapVoteReminderTimer?.Kill();
        _activeMapVoteReminderTimer = null;
        _rtvVotes.Clear();

        var groupedVotes = session.Options
            .Select(option => new
            {
                Option = option,
                Votes = session.VotesBySteamId.Values.Count(vote => string.Equals(vote, option.Key, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(group => group.Votes)
            .ThenBy(group => group.Option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topVoteCount = groupedVotes.FirstOrDefault()?.Votes ?? 0;
        var finalists = groupedVotes.Where(group => group.Votes == topVoteCount).Select(group => group.Option).ToList();
        var winningMap = finalists.Count > 0
            ? finalists[_random.Next(finalists.Count)]
            : session.Options[_random.Next(session.Options.Count)];

        var summary = string.Join("{Default}, {White}", groupedVotes.Select(group => $"{group.Option.DisplayName} ({group.Votes})"));
        Broadcast("{Gold}Map vote finished. Winning map: {White}" + winningMap.DisplayName + "{Gold}.");
        Broadcast("{Silver}Vote summary: {White}" + summary);
        QueueMapChange(winningMap);
    }

    private void ResetRtvState()
    {
        _rtvVotes.Clear();
        _activeMapVoteTimer?.Kill();
        _activeMapVoteTimer = null;
        _activeMapVoteReminderTimer?.Kill();
        _activeMapVoteReminderTimer = null;
        _activeMapVote = null;
    }

    private List<ServerMapOption> GetAvailableMaps()
    {
        var maps = new Dictionary<string, ServerMapOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredMap in Config.MapPool)
        {
            if (MapExists(configuredMap))
            {
                var option = CreateStandardMapOption(configuredMap);
                maps[option.Key] = option;
            }
        }

        foreach (var workshopMap in Config.WorkshopMaps)
        {
            var option = CreateWorkshopMapOption(workshopMap);
            maps[option.Key] = option;
        }

        if (maps.Count == 0)
        {
            var mapsDirectory = Path.Combine(Server.GameDirectory, "maps");
            if (Directory.Exists(mapsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(mapsDirectory, "*.vpk"))
                {
                    var mapName = Path.GetFileNameWithoutExtension(file);
                    if (MapExists(mapName))
                    {
                        var option = CreateStandardMapOption(mapName);
                        maps[option.Key] = option;
                    }
                }
            }
        }

        return maps.Values.OrderBy(static map => map.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool MapExists(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        try
        {
            if (Server.IsMapValid(mapName))
            {
                return true;
            }
        }
        catch
        {
        }

        var mapsDirectory = Path.Combine(Server.GameDirectory, "maps");
        return File.Exists(Path.Combine(mapsDirectory, $"{mapName}.vpk")) ||
               File.Exists(Path.Combine(mapsDirectory, $"{mapName}.bsp"));
    }

    private ServerMapOption CreateStandardMapOption(string mapName)
    {
        return new ServerMapOption(mapName, mapName, mapName, false);
    }

    private ServerMapOption CreateWorkshopMapOption(WorkshopMapOption workshopMap)
    {
        var key = "workshop:" + workshopMap.Id;
        return new ServerMapOption(key, workshopMap.Label, workshopMap.Id, true);
    }

    private bool TryResolveMapOption(string value, out ServerMapOption selectedMap)
    {
        selectedMap = GetAvailableMaps().FirstOrDefault(option =>
            string.Equals(option.Key, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.CommandTarget, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.DisplayName, value, StringComparison.OrdinalIgnoreCase))!;

        if (selectedMap is not null)
        {
            return true;
        }

        if (MapExists(value))
        {
            selectedMap = CreateStandardMapOption(value);
            return true;
        }

        return false;
    }

    private void QueueMapChange(ServerMapOption selectedMap)
    {
        SaveAllPlayerProgress();
        AddTimer(Config.MapChangeDelaySeconds, () =>
        {
            if (selectedMap.IsWorkshop)
            {
                Server.ExecuteCommand($"host_workshop_map {selectedMap.CommandTarget}");
            }
            else
            {
                Server.ExecuteCommand($"changelevel \"{selectedMap.CommandTarget}\"");
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private List<ServerMapOption> PickMapVoteOptions()
    {
        var currentMap = Server.MapName;
        var maps = GetAvailableMaps()
            .Where(map => map.IsWorkshop || !string.Equals(map.CommandTarget, currentMap, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => _random.Next())
            .Take(Config.RtvMapOptionCount)
            .ToList();

        if (maps.Count < 2)
        {
            maps = GetAvailableMaps().OrderBy(_ => _random.Next()).Take(Config.RtvMapOptionCount).ToList();
        }

        return maps;
    }

    private int GetRequiredRtvVotes()
    {
        return Math.Max(1, (int)Math.Ceiling(GetHumanPlayers().Count * Config.RtvRequiredRatio));
    }

    private void SubmitRtvVote(CCSPlayerController caller, ulong? knownSteamId = null)
    {
        if (!TryGetSteamId(caller, out var steamId) && knownSteamId is null)
        {
            return;
        }

        steamId = knownSteamId ?? steamId;
        if (_activeMapVote is not null)
        {
            OpenMapVoteMenu(caller);
            Reply(caller, "{Yellow}A map vote is already active. Pick a map from the menu.");
            return;
        }

        if (!_rtvVotes.Add(steamId))
        {
            Reply(caller, "{Yellow}You already voted to RTV this map.");
            return;
        }

        var requiredVotes = GetRequiredRtvVotes();
        Broadcast("{Yellow}" + caller.PlayerName + "{Default} voted to RTV. {White}" + _rtvVotes.Count + "{Default}/{White}" + requiredVotes + "{Default} votes.");

        if (_rtvVotes.Count >= requiredVotes)
        {
            StartMapVote(caller, false);
        }
    }

    private void BroadcastVoteReminder()
    {
        if (_activeMapVote is null)
        {
            return;
        }

        Broadcast("{Yellow}Map vote ends in {White}" + Config.RtvReminderSeconds + "{Yellow}s. Use {White}!vote{Yellow} or {White}!rtv{Yellow} to reopen the menu.");
    }

    private bool ChangeMapTo(string mapName, string actorName, CommandInfo? command = null)
    {
        if (!TryResolveMapOption(mapName, out var selectedMap))
        {
            if (command is not null)
            {
                Reply(command, "{Red}Unknown or unavailable map: {White}" + mapName);
            }

            return false;
        }

        Broadcast("{Gold}" + actorName + "{Default} selected map {White}" + selectedMap.DisplayName + "{Default}.");
        QueueMapChange(selectedMap);
        return true;
    }

    private void ApplyGameMode(GameModeOption mode, string actorName)
    {
        _forcedLoadoutModeEnabled = false;
        SaveAllPlayerProgress();
        var nextMap = ResolvePreferredModeMap(mode);
        Broadcast("{Gold}" + actorName + "{Default} switched the server to {White}" + mode.Label + "{Default}. Loading {White}" + nextMap + "{Default}.");
        Server.ExecuteCommand($"game_alias {mode.Alias}");

        var (gameType, gameMode, mapGroup) = ResolveModeConVars(mode.Alias);
        if (gameType is not null)
        {
            Server.ExecuteCommand($"game_type {gameType.Value}");
        }

        if (gameMode is not null)
        {
            Server.ExecuteCommand($"game_mode {gameMode.Value}");
        }

        if (!string.IsNullOrWhiteSpace(mapGroup))
        {
            Server.ExecuteCommand($"mapgroup {mapGroup}");
        }

        AddTimer(Config.MapChangeDelaySeconds, () => Server.ExecuteCommand($"map \"{nextMap}\""), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void EnableForcedLoadoutMode(ForcedLoadoutType loadout, string actorName, CommandInfo? command = null, CCSPlayerController? actor = null)
    {
        _forcedLoadoutModeEnabled = true;
        _forcedLoadout = loadout;
        SaveAllPlayerProgress();

        var nextMap = ResolvePreferredModeMap(new GameModeOption
        {
            Label = "Deathmatch",
            Alias = "deathmatch"
        });

        Broadcast("{Gold}" + actorName + "{Default} enabled {White}" + GetForcedLoadoutLabel(loadout) + "{Default} loadout mode for all players.");
        ApplyDeathmatchModeCommands();
        ApplyForcedLoadoutServerRules();
        ReapplyForcedLoadoutToAlivePlayers();

        ReplyLoadoutCommandResult(command, actor, "{Green}Forced loadout is now {White}" + GetForcedLoadoutLabel(loadout) + "{Green} for all players.");
        AddTimer(Config.MapChangeDelaySeconds, () => Server.ExecuteCommand($"map \"{nextMap}\""), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void DisableForcedLoadoutMode(string actorName, CommandInfo? command = null, CCSPlayerController? actor = null)
    {
        if (!_forcedLoadoutModeEnabled)
        {
            ReplyLoadoutCommandResult(command, actor, "{Yellow}Forced loadout mode is already disabled.");
            return;
        }

        _forcedLoadoutModeEnabled = false;
        ReplyLoadoutCommandResult(command, actor, "{Green}Forced loadout mode is disabled. Returning to standard Deathmatch.");

        var deathmatchMode = Config.GameModes.FirstOrDefault(mode =>
            mode.Alias.Contains("deathmatch", StringComparison.OrdinalIgnoreCase) ||
            mode.Alias.Equals("dm", StringComparison.OrdinalIgnoreCase) ||
            mode.Label.Contains("deathmatch", StringComparison.OrdinalIgnoreCase));

        if (deathmatchMode is not null)
        {
            ApplyGameMode(deathmatchMode, actorName);
            return;
        }

        SaveAllPlayerProgress();
        Broadcast("{Gold}" + actorName + "{Default} disabled forced loadout mode and restored standard Deathmatch.");
        ApplyDeathmatchModeCommands();
        AddTimer(Config.MapChangeDelaySeconds, () => Server.ExecuteCommand($"map \"{ResolvePreferredModeMap(new GameModeOption { Label = "Deathmatch", Alias = "deathmatch" })}\""), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private bool TryGetGameMode(string value, out GameModeOption gameMode)
    {
        gameMode = Config.GameModes.FirstOrDefault(mode =>
            string.Equals(mode.Alias, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode.Label, value, StringComparison.OrdinalIgnoreCase))!;

        return gameMode is not null;
    }

    private (int XpToAward, string Reason) GetKillXpAward(CCSPlayerController victim, bool knifeKill, bool headshot)
    {
        var xpToAward = GetBaseKillXpForCurrentMode();
        var reason = victim.IsBot ? "bot kill" : "kill";

        if (knifeKill)
        {
            xpToAward += Config.KnifeKillBonusXp;
            reason = "knife " + reason;
        }

        if (headshot)
        {
            xpToAward += Config.HeadshotBonusXp;
            reason += " + headshot";
        }

        if (victim.IsBot)
        {
            xpToAward = (int)Math.Round(xpToAward * Config.BotXpMultiplier, MidpointRounding.AwayFromZero);
        }

        return (Math.Max(0, xpToAward), reason);
    }

    private int GetBaseKillXpForCurrentMode()
    {
        return IsFastXpMode(GetCurrentGameModeAlias()) ? Config.FastModeKillXp : Config.CasualCompetitiveKillXp;
    }

    private string GetCurrentXpModeLabel()
    {
        var alias = GetCurrentGameModeAlias();
        if (alias.Contains("armsrace", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("arms race", StringComparison.OrdinalIgnoreCase))
        {
            return "Arms Race";
        }

        if (IsDeathmatchActive(alias))
        {
            return "Deathmatch";
        }

        return "Casual / Competitive";
    }

    private string GetCurrentGameModeAlias()
    {
        var alias = ConVar.Find("game_alias")?.StringValue;
        if (!string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim().ToLowerInvariant();
        }

        if (IsDeathmatchActive(string.Empty))
        {
            return "deathmatch";
        }

        return "casual";
    }

    private bool IsFastXpMode(string alias)
    {
        return IsDeathmatchActive(alias) ||
               alias.Contains("armsrace", StringComparison.OrdinalIgnoreCase) ||
               alias.Contains("arms race", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDeathmatchActive(string alias)
    {
        return alias.Contains("deathmatch", StringComparison.OrdinalIgnoreCase) ||
               alias.Equals("dm", StringComparison.OrdinalIgnoreCase) ||
               GetConVarBool("mp_teammates_are_enemies") ||
               GetConVarInt("mp_dm_teammode") > 0;
    }

    private bool CanAwardKillXp(CCSPlayerController killer, CCSPlayerController victim)
    {
        if (killer == victim)
        {
            return false;
        }

        if (GetConVarBool("mp_teammates_are_enemies"))
        {
            return true;
        }

        return killer.Team != victim.Team;
    }

    private bool IsChickenTarget(CCSPlayerController player)
    {
        return player is { IsValid: true } &&
               player.IsBot &&
               player.Team is not CsTeam.Terrorist and not CsTeam.CounterTerrorist &&
               player.PlayerName.Contains("chicken", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseForcedLoadout(string value, out ForcedLoadoutType loadout, out bool disable)
    {
        var normalized = value.Trim().ToLowerInvariant();
        disable = normalized is "off" or "disable" or "disabled" or "normal";
        loadout = normalized switch
        {
            "rifle" or "rifles" => ForcedLoadoutType.Rifle,
            "pistol" or "pistols" => ForcedLoadoutType.Pistol,
            "knife" or "knives" => ForcedLoadoutType.Knife,
            _ => ForcedLoadoutType.Rifle
        };

        return disable || normalized is "rifle" or "rifles" or "pistol" or "pistols" or "knife" or "knives";
    }

    private string ResolvePreferredModeMap(GameModeOption mode)
    {
        var alias = mode.Alias.Trim().ToLowerInvariant();
        var currentMap = Server.MapName;
        if (IsMapCompatibleWithMode(currentMap, alias))
        {
            return currentMap;
        }

        if (alias.Contains("armsrace", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("arms race", StringComparison.OrdinalIgnoreCase))
        {
            return Config.MapPool.FirstOrDefault(map => map.StartsWith("ar_", StringComparison.OrdinalIgnoreCase) && MapExists(map))
                   ?? "ar_shoots";
        }

        return Config.MapPool.FirstOrDefault(map =>
                   (map.StartsWith("de_", StringComparison.OrdinalIgnoreCase) || map.StartsWith("cs_", StringComparison.OrdinalIgnoreCase)) &&
                   MapExists(map))
               ?? "de_dust2";
    }

    private bool IsMapCompatibleWithMode(string mapName, string alias)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        if (alias.Contains("armsrace", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("arms race", StringComparison.OrdinalIgnoreCase))
        {
            return mapName.StartsWith("ar_", StringComparison.OrdinalIgnoreCase);
        }

        if (alias.Contains("casual", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("competitive", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("deathmatch", StringComparison.OrdinalIgnoreCase) ||
            alias.Equals("dm", StringComparison.OrdinalIgnoreCase))
        {
            return mapName.StartsWith("de_", StringComparison.OrdinalIgnoreCase) ||
                   mapName.StartsWith("cs_", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private (int? GameType, int? GameMode, string? MapGroup) ResolveModeConVars(string alias)
    {
        alias = alias.Trim().ToLowerInvariant();
        if (alias.Contains("armsrace", StringComparison.OrdinalIgnoreCase) ||
            alias.Contains("arms race", StringComparison.OrdinalIgnoreCase))
        {
            return (1, 0, "mg_armsrace");
        }

        if (alias.Contains("deathmatch", StringComparison.OrdinalIgnoreCase) || alias == "dm")
        {
            return (1, 2, "mg_active");
        }

        if (alias.Contains("competitive", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 1, "mg_active");
        }

        if (alias.Contains("casual", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0, "mg_active");
        }

        return (null, null, null);
    }

    private void KickPlayer(CCSPlayerController target, string actorName)
    {
        Broadcast("{Red}" + target.PlayerName + "{Default} was kicked by {White}" + actorName + "{Default}. Reason: {White}" + Config.KickReason);
        target.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
    }

    private void KickAllBots(string actorName, CommandInfo? command = null, CCSPlayerController? actor = null)
    {
        var botCount = GetBotPlayers().Count;
        Server.ExecuteCommand("bot_quota 0");
        Server.ExecuteCommand("bot_kick");

        var suffix = botCount == 1 ? "bot" : "bots";
        Broadcast("{Gold}" + actorName + "{Default} set bot quota to {White}0{Default} and removed {White}" + botCount + "{Default} " + suffix + ".");

        var replyMessage = "{Green}Bot quota is now {White}0{Green}. Removed {White}" + botCount + "{Green} " + suffix + ".";
        if (IsRealPlayer(actor))
        {
            Reply(actor!, replyMessage);
            return;
        }

        if (command is not null)
        {
            Reply(command, replyMessage);
        }
    }

    private void AddBots(string actorName, int desiredBots, CommandInfo? command = null, CCSPlayerController? actor = null)
    {
        var availableSlots = Math.Max(0, Server.MaxPlayers - GetHumanPlayers().Count);
        var botCount = Math.Clamp(desiredBots, 0, availableSlots);
        Server.ExecuteCommand("bot_join_after_player 0");
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand($"bot_quota {botCount}");

        var suffix = botCount == 1 ? "bot" : "bots";
        Broadcast("{Gold}" + actorName + "{Default} set the bot quota to {White}" + botCount + "{Default} " + suffix + ".");

        var replyMessage = botCount == desiredBots
            ? "{Green}Bot quota is now {White}" + botCount + "{Green} " + suffix + "."
            : "{Yellow}Bot quota is now {White}" + botCount + "{Yellow} " + suffix + "{Yellow} because only {White}" + availableSlots + "{Yellow} slots are free.";

        if (IsRealPlayer(actor))
        {
            Reply(actor!, replyMessage);
            return;
        }

        if (command is not null)
        {
            Reply(command, replyMessage);
        }
    }

    private void CancelActiveVote(string actorName, CommandInfo? command = null)
    {
        if (_activeMapVote is null)
        {
            if (command is not null)
            {
                Reply(command, "{Yellow}There is no active vote to cancel.");
            }

            return;
        }

        ResetRtvState();
        Broadcast("{Red}" + actorName + "{Default} canceled the active map vote.");
        if (command is not null)
        {
            Reply(command, "{Red}Active vote canceled.");
        }
    }

    private bool AdjustXp(CCSPlayerController player, long delta, string reason, bool showDeltaMessage)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null || delta == 0 || _repository is null)
        {
            return false;
        }

        var oldState = _levelCurve.GetState(progress.TotalXp);
        var newTotalXp = Math.Clamp(progress.TotalXp + delta, 0, _levelCurve.MaxTotalXp);
        var actualDelta = newTotalXp - progress.TotalXp;
        if (actualDelta == 0)
        {
            return false;
        }

        progress.TotalXp = newTotalXp;
        progress.PlayerName = NormalizeStoredPlayerName(player.PlayerName);
        _repository.SavePlayer(progress);
        Logger.LogInformation("XPX saved {TotalXp} XP for {SteamId} after {Reason} (delta {Delta})", progress.TotalXp, progress.SteamId, reason, actualDelta);

        var newState = _levelCurve.GetState(progress.TotalXp);
        if (showDeltaMessage)
        {
            var verb = actualDelta > 0 ? "gained" : "lost";
            var amount = Math.Abs(actualDelta).ToString("N0", CultureInfo.InvariantCulture);
            Reply(player,
                (actualDelta > 0 ? "{Green}" : "{Red}") + "You " + verb + " {White}" + amount + "{Default} XP for " + reason +
                ". {Silver}" + RenderShortLevelLabel(newState.Level) + " | " + RenderProgressPercent(newState));
        }

        HandleLevelTransitions(player, oldState, newState);
        return true;
    }

    private void HandleLevelTransitions(CCSPlayerController player, LevelState oldState, LevelState newState)
    {
        if (newState.Level == oldState.Level)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        ApplyRewardState(player, progress);

        if (newState.Level > oldState.Level)
        {
            var unlockedRewards = Config.Rewards
                .Where(reward => reward.Level > oldState.Level && reward.Level <= newState.Level)
                .Select(DescribeReward)
                .ToList();

            var rewardText = unlockedRewards.Count == 0
                ? string.Empty
                : "{Green} Unlocks: {White}" + string.Join("{Default}, {White}", unlockedRewards);
            var nextReward = GetNextReward(newState.Level);
            var nextRewardText = nextReward is null ? string.Empty : "{Silver} Next: {White}" + DescribeReward(nextReward);

            ShowLevelUpPanel(player, BuildLevelUpHtml(newState, unlockedRewards, nextReward), TransientPanelDurationSeconds);
            Broadcast("{Gold}" + player.PlayerName + "{Default} reached level {White}" + newState.Level + "{Default}." + rewardText + nextRewardText);
        }
        else
        {
            Reply(player, "{Yellow}Your level changed to {White}" + newState.Level + "{Yellow}.");
        }
    }

    private void ApplyRewardState(CCSPlayerController player, PlayerProgress progress, bool refreshEquipment = true)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        var reward = GetCurrentReward(_levelCurve.GetState(progress.TotalXp).Level);
        var tag = reward?.Tag ?? string.Empty;
        var cleanName = NormalizeStoredPlayerName(progress.PlayerName);
        if (!string.Equals(progress.PlayerName, cleanName, StringComparison.Ordinal))
        {
            progress.PlayerName = cleanName;
        }

        if (!string.IsNullOrEmpty(player.Clan))
        {
            player.Clan = string.Empty;
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
        }

        var displayName = BuildDisplayPlayerName(cleanName, tag);
        if (!string.Equals(player.PlayerName, displayName, StringComparison.Ordinal))
        {
            player.PlayerName = displayName;
            Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
        }

        if (!refreshEquipment || !player.PawnIsAlive)
        {
            return;
        }

        ApplyEquipmentState(player, progress, reward);
    }

    private LevelReward? GetCurrentReward(int level)
    {
        return Config.Rewards.LastOrDefault(reward => reward.Level <= level);
    }

    private LevelReward? GetNextReward(int level)
    {
        return Config.Rewards.FirstOrDefault(reward => reward.Level > level);
    }

    private string DescribeReward(LevelReward reward)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(reward.Tag))
        {
            pieces.Add($"tag {reward.Tag}");
        }

        if (!string.IsNullOrWhiteSpace(reward.KnifeItem))
        {
            pieces.Add(reward.KnifeItem.Replace("weapon_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace('_', ' '));
        }

        return pieces.Count == 0 ? $"level {reward.Level} reward" : string.Join(" + ", pieces);
    }

    private string ExpandTokens(CCSPlayerController player, PlayerProgress progress, string message)
    {
        var state = _levelCurve.GetState(progress.TotalXp);
        var nextReward = GetNextReward(state.Level);
        var xpRemaining = state.XpNeededForNextLevel == 0 ? 0 : state.XpNeededForNextLevel - state.XpIntoLevel;

        return message
            .Replace("{NAME}", player.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{SERVER}", Config.ServerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{LEVEL}", state.Level.ToString("N0", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{TOTAL_XP}", progress.TotalXp.ToString("N0", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{NEXT_LEVEL}", Math.Min(Config.MaxLevel, state.Level + 1).ToString("N0", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{XP_NEEDED}", xpRemaining.ToString("N0", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{NEXT_REWARD}", nextReward is null ? "All rewards unlocked" : DescribeReward(nextReward), StringComparison.OrdinalIgnoreCase);
    }

    private bool IsKnifeKill(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return false;
        }

        var normalized = weaponName.ToLowerInvariant();
        return normalized.Contains("knife", StringComparison.Ordinal) ||
               normalized.Contains("bayonet", StringComparison.Ordinal) ||
               normalized.Contains("dagger", StringComparison.Ordinal) ||
               normalized.Contains("karambit", StringComparison.Ordinal) ||
               normalized.Contains("talon", StringComparison.Ordinal) ||
               normalized.Contains("butterfly", StringComparison.Ordinal);
    }

    private PlayerProgress? EnsurePlayerProgress(CCSPlayerController? player, bool reloadFromRepository = false)
    {
        if (!TryGetSteamId(player, out var steamId) || _repository is null)
        {
            return null;
        }

        var cleanName = NormalizeStoredPlayerName(player!.PlayerName);
        if (_players.TryGetValue(steamId, out var cachedProgress) && !reloadFromRepository)
        {
            cachedProgress.PlayerName = cleanName;
            _slotToSteamId[player.Slot] = steamId;
            return cachedProgress;
        }

        var storedProgress = _repository.GetPlayer(steamId);
        _transitionSnapshotBySteamId.TryGetValue(steamId, out var snapshotEntry);
        var snapshotXp = snapshotEntry?.TotalXp ?? 0;
        var resolvedXp = Math.Max(storedProgress?.TotalXp ?? 0, snapshotXp);
        var loadedProgress = storedProgress ?? new PlayerProgress
        {
            SteamId = steamId,
            PlayerName = cleanName,
            TotalXp = resolvedXp
        };
        loadedProgress.PlayerName = cleanName;
        loadedProgress.TotalXp = resolvedXp;

        if (_players.TryGetValue(steamId, out var progress))
        {
            progress.PlayerName = cleanName;
            if (reloadFromRepository)
            {
                progress.TotalXp = loadedProgress.TotalXp;
                progress.Credits = loadedProgress.Credits;
                progress.CrateTokens = loadedProgress.CrateTokens;
            }
        }
        else
        {
            progress = loadedProgress;
            if (storedProgress is null)
            {
                Logger.LogInformation("XPX created a new progress row for {SteamId} ({PlayerName})", steamId, cleanName);
            }
            else
            {
                Logger.LogInformation("XPX reloaded {TotalXp} XP for {SteamId} ({PlayerName})", storedProgress.TotalXp, steamId, cleanName);
            }
        }

        if (storedProgress is null || storedProgress.TotalXp < loadedProgress.TotalXp)
        {
            _repository.SavePlayer(progress);
        }

        _players[steamId] = progress;
        _slotToSteamId[player.Slot] = steamId;
        EnsureFeatureStateLoaded(player);
        return progress;
    }

    private bool TryGetSteamId(CCSPlayerController? player, out ulong steamId)
    {
        steamId = 0;
        if (!IsRealPlayer(player))
        {
            return false;
        }

        if (player!.AuthorizedSteamID is not null)
        {
            steamId = player.AuthorizedSteamID.SteamId64;
            if (steamId > 0)
            {
                _slotToSteamId[player.Slot] = steamId;
                return true;
            }
        }

        steamId = player.SteamID;
        if (steamId > 0)
        {
            _slotToSteamId[player.Slot] = steamId;
            return true;
        }

        if (_slotToSteamId.TryGetValue(player.Slot, out steamId) && steamId > 0)
        {
            return true;
        }

        return steamId > 0;
    }

    private bool IsRealPlayer(CCSPlayerController? player)
    {
        return player is { IsValid: true } &&
               player.Connected == PlayerConnectedState.PlayerConnected &&
               !player.IsBot &&
               !player.IsHLTV;
    }

    private bool IsXpEligibleTarget(CCSPlayerController? player)
    {
        return player is { IsValid: true } &&
               player.Connected == PlayerConnectedState.PlayerConnected &&
               !player.IsHLTV;
    }

    private List<CCSPlayerController> GetHumanPlayers()
    {
        return Utilities.GetPlayers()
            .Where(IsRealPlayer)
            .OrderBy(static player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<CCSPlayerController> GetBotPlayers()
    {
        return Utilities.GetPlayers()
            .Where(player => player is { IsValid: true } &&
                             player.Connected == PlayerConnectedState.PlayerConnected &&
                             player.IsBot &&
                             !player.IsHLTV)
            .OrderBy(static player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void PrintStatBlock(CCSPlayerController player, string title, params string[] lines)
    {
        PrintStatBlock(player, title, (IEnumerable<string>)lines);
    }

    private void PrintStatBlock(CCSPlayerController player, string title, IEnumerable<string> lines)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        player.PrintToChat(FormatChat($"{Config.ChatPrefix} {{Gold}}{title}"));
        foreach (var line in lines)
        {
            player.PrintToChat(FormatChat($"{Config.ChatPrefix} {line}"));
        }
    }

    private string RenderProgressPercent(LevelState state)
    {
        if (state.XpNeededForNextLevel <= 0)
        {
            return "MAX";
        }

        var percent = (double)state.XpIntoLevel / state.XpNeededForNextLevel * 100d;
        return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private void ShowLevelOverview(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var state = _levelCurve.GetState(progress.TotalXp);
        var xpRemaining = state.XpNeededForNextLevel == 0 ? 0 : state.XpNeededForNextLevel - state.XpIntoLevel;
        var nextReward = GetNextReward(state.Level);
        var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);

        PrintStatBlock(player,
            "XPX Level",
            "{White}Level: {Gold}" + state.Level.ToString("N0", CultureInfo.InvariantCulture) + "{Default}/{Gold}" + Config.MaxLevel.ToString("N0", CultureInfo.InvariantCulture),
            "{White}Tag: {Gold}" + GetCurrentVisibleTag(progress),
            "{White}Progress: {Green}" + RenderProgressPercent(state) + "{Default} (" + state.XpIntoLevel.ToString("N0", CultureInfo.InvariantCulture) + "/" + state.XpNeededForNextLevel.ToString("N0", CultureInfo.InvariantCulture) + " XP)",
            "{White}Server rank: {Gold}#" + rank.ToString("N0", CultureInfo.InvariantCulture) + "{Default}/{Gold}" + totalPlayers.ToString("N0", CultureInfo.InvariantCulture),
            "{White}Next level in: {Gold}" + xpRemaining.ToString("N0", CultureInfo.InvariantCulture) + "{Default} XP",
            "{White}Next reward: {Gold}" + (nextReward is null ? "All rewards unlocked" : DescribeReward(nextReward)));
    }

    private void OpenLevelOverviewMenu(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var state = _levelCurve.GetState(progress.TotalXp);
        var xpRemaining = state.XpNeededForNextLevel == 0 ? 0 : state.XpNeededForNextLevel - state.XpIntoLevel;
        var nextReward = GetNextReward(state.Level);
        var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);

        OpenReadOnlyMenu(player,
            "My Level",
            new[]
            {
                $"Level: {state.Level}/{Config.MaxLevel}",
                $"Tag: {GetCurrentVisibleTag(progress)}",
                $"Progress: {RenderProgressPercent(state)} ({state.XpIntoLevel}/{state.XpNeededForNextLevel} XP)",
                $"Rank: #{rank}/{totalPlayers}",
                $"Next level in: {xpRemaining} XP",
                "Next reward: " + (nextReward is null ? "All rewards unlocked" : DescribeReward(nextReward))
            });
    }

    private void ShowRankOverview(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var state = _levelCurve.GetState(progress.TotalXp);
        var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);
        PrintStatBlock(player,
            "XPX Rank",
            "{White}Current rank: {Gold}#" + rank.ToString("N0", CultureInfo.InvariantCulture),
            "{White}Tracked players: {Gold}" + totalPlayers.ToString("N0", CultureInfo.InvariantCulture),
            "{White}Level: {Gold}" + state.Level.ToString("N0", CultureInfo.InvariantCulture),
            "{White}Tag: {Gold}" + GetCurrentVisibleTag(progress),
            "{White}Total XP: {Gold}" + progress.TotalXp.ToString("N0", CultureInfo.InvariantCulture));
    }

    private void OpenRankOverviewMenu(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var state = _levelCurve.GetState(progress.TotalXp);
        var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);

        OpenReadOnlyMenu(player,
            "My Rank",
            new[]
            {
                $"Current rank: #{rank}",
                $"Tracked players: {totalPlayers}",
                $"Level: {state.Level}",
                $"Tag: {GetCurrentVisibleTag(progress)}",
                $"Total XP: {progress.TotalXp:N0}"
            });
    }

    private void ShowTopOverview(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        var topPlayers = _repository.GetTopPlayers(Config.TopCount);
        if (topPlayers.Count == 0)
        {
            Reply(player, "{Yellow}No ranked players yet. Start fragging.");
            return;
        }

        var lines = topPlayers.Select(entry =>
        {
            var level = _levelCurve.GetState(entry.TotalXp).Level;
            var marker = progress is not null && entry.SteamId == progress.SteamId ? "{Gold}>" : "{White}#";
            return marker + entry.Rank.ToString(CultureInfo.InvariantCulture) +
                   "{Default} " + entry.PlayerName +
                   " {Silver}" + RenderShortLevelLabel(level) +
                   " {Green}" + entry.TotalXp.ToString("N0", CultureInfo.InvariantCulture) + " XP";
        }).ToList();

        if (progress is not null)
        {
            var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);
            if (rank > topPlayers.Count)
            {
                lines.Add("{White}You: {Gold}#" + rank.ToString("N0", CultureInfo.InvariantCulture) +
                          "{Default}/{Gold}" + totalPlayers.ToString("N0", CultureInfo.InvariantCulture) +
                          " {Green}" + progress.TotalXp.ToString("N0", CultureInfo.InvariantCulture) + " XP");
            }
        }

        PrintStatBlock(player, $"Top {Config.TopCount}", lines);
    }

    private void OpenTopOverviewMenu(CCSPlayerController player)
    {
        if (_repository is null)
        {
            return;
        }

        var progress = EnsurePlayerProgress(player);
        var topPlayers = _repository.GetTopPlayers(Config.TopCount);
        if (topPlayers.Count == 0)
        {
            OpenReadOnlyMenu(player, "Top Players", new[] { "No ranked players yet. Start fragging." });
            return;
        }

        var lines = topPlayers.Select(entry =>
        {
            var level = _levelCurve.GetState(entry.TotalXp).Level;
            var marker = progress is not null && entry.SteamId == progress.SteamId ? "> " : "#";
            return $"{marker}{entry.Rank} {entry.PlayerName} | LVL {level} | {entry.TotalXp:N0} XP";
        }).ToList();

        if (progress is not null)
        {
            var (rank, totalPlayers) = _repository.GetRank(progress.SteamId, progress.TotalXp);
            if (rank > topPlayers.Count)
            {
                lines.Add($"You: #{rank}/{totalPlayers} | {progress.TotalXp:N0} XP");
            }
        }

        OpenReadOnlyMenu(player, $"Top {Config.TopCount}", lines);
    }

    private void ToggleHelpPanel(CCSPlayerController player)
    {
        if (!TryGetSteamId(player, out var steamId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastHelpToggleAt.TryGetValue(steamId, out var lastToggleAt) &&
            (now - lastToggleAt).TotalMilliseconds < 350d)
        {
            return;
        }

        _lastHelpToggleAt[steamId] = now;

        if (_openHelpPanels.Contains(steamId))
        {
            CloseHelpPanel(player);
            return;
        }

        ShowHelpPanel(player, steamId);
    }

    private void ShowHelpPanel(CCSPlayerController player, ulong steamId, bool closeActiveMenu = true)
    {
        if (closeActiveMenu)
        {
            CloseActiveXPXMenu(player);
        }

        StopHelpPanel(steamId);
        _openHelpPanels.Add(steamId);
        var panelPlayer = player;
        panelPlayer.PrintToCenterHtml(BuildHelpHtml(panelPlayer));

        CounterStrikeSharp.API.Core.Listeners.OnTick? tickHandler = null;
        tickHandler = () =>
        {
            if (!_openHelpPanels.Contains(steamId))
            {
                if (tickHandler is not null)
                {
                    RemoveListener(tickHandler);
                }
                return;
            }

            if (!IsRealPlayer(panelPlayer))
            {
                if (tickHandler is not null)
                {
                    RemoveListener(tickHandler);
                }
                return;
            }

            if (MenuManager.GetActiveMenu(panelPlayer) is null)
            {
                panelPlayer.PrintToCenterHtml(BuildHelpHtml(panelPlayer));
            }
        };

        RegisterListener(tickHandler);
        _helpTickHandlers[steamId] = tickHandler;
        _helpTimers[steamId] = AddTimer(TransientPanelDurationSeconds, () => CloseHelpPanel(steamId), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void CloseHelpPanel(CCSPlayerController player)
    {
        if (!TryGetSteamId(player, out var steamId))
        {
            return;
        }

        CloseHelpPanel(steamId);
        if (IsRealPlayer(player))
        {
            player.PrintToCenterHtml(" ");
        }
    }

    private void CloseHelpPanel(ulong steamId)
    {
        StopHelpPanel(steamId);
        var player = Utilities.GetPlayerFromSteamId64(steamId);
        if (IsRealPlayer(player))
        {
            player!.PrintToCenterHtml(" ");
        }
    }

    private string BuildHelpHtml(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        var tag = GetCurrentVisibleTag(progress);
        var nextReward = progress is null ? null : GetNextReward(_levelCurve.GetState(progress.TotalXp).Level);
        var currentMode = GetCurrentXpModeLabel();
        var nextRewardText = nextReward is null ? "All rewards unlocked" : DescribeReward(nextReward);
        var lines = new List<string>
        {
            "<b><font color='gold'>XPX Help</font></b>",
            $"<font color='white'>Current mode:</font> <font color='gold'>{currentMode}</font>",
            $"<font color='white'>Level cap:</font> <font color='gold'>{Config.MaxLevel}</font>",
            $"<font color='white'>Your tag:</font> <font color='gold'>{tag}</font>",
            "<font color='white'>How to play:</font> <font color='gold'>Get kills, win rounds, do objectives, and finish missions to earn XP and credits.</font>",
            "<font color='white'>Warmup:</font> <font color='gold'>No XP is awarded during warmup.</font>",
            "<font color='white'>Start here:</font> <font color='gold'>!me !help !level !rank !top !stats</font>",
            "<font color='white'>Goals:</font> <font color='gold'>!missions !achievements</font>",
            $"<font color='white'>Economy:</font> <font color='gold'>Earn {Config.CurrencyName} from missions, achievements, streaks, and bonuses, then spend them in !shop or !crate.</font>",
            "<font color='white'>Other commands:</font> <font color='gold'>!wallet !rtv !vote !gamble &lt;xp&gt;</font>",
            "<font color='white'>Progression:</font> <font color='gold'>Level to 500 and unlock permanent tags plus knife rewards.</font>",
            $"<font color='white'>Next reward:</font> <font color='gold'>{nextRewardText}</font>",
            "<font color='white'>Menu input:</font> <font color='gold'>Use !bindmenu for 1-9 keys, or type !1-!9 in chat while a menu is open</font>",
            "<font color='deepskyblue'>Type !help again to close, or wait a few seconds.</font>"
        };

        if (HasPermission(player, PermissionMenu) || HasPermission(player, PermissionKick))
        {
            lines.Insert(lines.Count - 2, "<font color='white'>Admin commands:</font> <font color='gold'>!admin !kickbots !addbots [count] !kniferound !pistolround !warmupevent</font>");
        }

        return string.Join("<br>", lines);
    }

    private void OpenXPXMenu(CCSPlayerController player, XPXNumberMenu menu)
    {
        CloseHelpPanel(player);
        menu.Open(player);
    }

    private void OpenReadOnlyMenu(CCSPlayerController player, string title, IEnumerable<string> lines)
    {
        var menu = CreateMenu(title);
        menu.PostSelectAction = PostSelectAction.Close;

        foreach (var line in lines)
        {
            menu.AddMenuOption(line, static (_, _) => { }, disabled: true);
        }

        menu.AddMenuOption("Back to me", (target, _) => OpenMeMenu(target));
        OpenXPXMenu(player, menu);
    }

    private bool TryHandleMenuChatSelection(CCSPlayerController player, CommandInfo command)
    {
        if (MenuManager.GetActiveMenu(player) is null)
        {
            return false;
        }

        var chatToken = ExtractChatToken(command);
        if (chatToken.Length != 2 || (chatToken[0] != '!' && chatToken[0] != '/'))
        {
            return false;
        }

        if (!int.TryParse(chatToken[1].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var key))
        {
            return false;
        }

        if (key < 1 || key > 9)
        {
            return false;
        }

        MenuManager.OnKeyPress(player, key);
        return true;
    }

    private static string ExtractChatToken(CommandInfo command)
    {
        var raw = command.GetCommandString?.Trim() ?? string.Empty;
        if (raw.StartsWith("say_team ", StringComparison.OrdinalIgnoreCase))
        {
            return raw["say_team ".Length..].Trim().Trim('"');
        }

        if (raw.StartsWith("say ", StringComparison.OrdinalIgnoreCase))
        {
            return raw["say ".Length..].Trim().Trim('"');
        }

        return (command.ArgString ?? string.Empty).Trim().Trim('"');
    }

    private static bool GetConVarBool(string name, bool fallback = false)
    {
        try
        {
            var conVar = ConVar.Find(name);
            if (conVar is null)
            {
                return fallback;
            }

            return conVar.Type switch
            {
                ConVarType.Bool => conVar.GetPrimitiveValue<bool>(),
                ConVarType.Int16 => conVar.GetPrimitiveValue<short>() != 0,
                ConVarType.UInt16 => conVar.GetPrimitiveValue<ushort>() != 0,
                ConVarType.Int32 => conVar.GetPrimitiveValue<int>() != 0,
                ConVarType.UInt32 => conVar.GetPrimitiveValue<uint>() != 0,
                ConVarType.Int64 => conVar.GetPrimitiveValue<long>() != 0,
                ConVarType.UInt64 => conVar.GetPrimitiveValue<ulong>() != 0,
                ConVarType.String => bool.TryParse(conVar.StringValue, out var parsedBool) ? parsedBool : fallback,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static int GetConVarInt(string name, int fallback = 0)
    {
        try
        {
            var conVar = ConVar.Find(name);
            if (conVar is null)
            {
                return fallback;
            }

            return conVar.Type switch
            {
                ConVarType.Bool => conVar.GetPrimitiveValue<bool>() ? 1 : 0,
                ConVarType.Int16 => conVar.GetPrimitiveValue<short>(),
                ConVarType.UInt16 => conVar.GetPrimitiveValue<ushort>(),
                ConVarType.Int32 => conVar.GetPrimitiveValue<int>(),
                ConVarType.UInt32 => unchecked((int)conVar.GetPrimitiveValue<uint>()),
                ConVarType.Int64 => unchecked((int)conVar.GetPrimitiveValue<long>()),
                ConVarType.UInt64 => unchecked((int)conVar.GetPrimitiveValue<ulong>()),
                ConVarType.String => int.TryParse(conVar.StringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) ? parsedInt : fallback,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private void ApplyPersistentMenuKeyBinds(CCSPlayerController player)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        for (var key = 1; key <= 9; key++)
        {
            TryBindMenuKeyFromServer(player, key);
        }

        AddTimer(0.15f, () =>
        {
            if (!IsRealPlayer(player))
            {
                return;
            }

            TryExecuteClientCommandFromServer(player, "host_writeconfig");
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void TryBindMenuKeyFromServer(CCSPlayerController player, int key)
    {
        TryExecuteClientCommandFromServer(player, $"bind {key} \"slot{key};css_{key}\"");
    }

    private static void TryExecuteClientCommandFromServer(CCSPlayerController player, string command)
    {
        try
        {
            player.ExecuteClientCommandFromServer(command);
        }
        catch
        {
            // Some clients or commands may still be blocked depending on engine state.
        }
    }

    private bool IsHelpCommand(CommandInfo command)
    {
        var commandName = command.ArgCount > 0 ? command.GetArg(0)?.Trim() ?? string.Empty : string.Empty;
        if (string.Equals(commandName, "css_help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandName, "help", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var raw = command.GetCommandString?.Trim() ?? string.Empty;
        if (string.Equals(raw, "say !help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "say /help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "say_team !help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "say_team /help", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var argString = command.ArgString?.Trim() ?? string.Empty;
        return string.Equals(argString, "!help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argString, "/help", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsWarmupActive()
    {
        try
        {
            var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault(proxy => proxy is { IsValid: true })?
                .GameRules;
            return rules is not null && rules.WarmupPeriod;
        }
        catch
        {
            return false;
        }
    }

    private string RenderShortLevelLabel(int level)
    {
        return "LVL " + level.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void StopHelpPanel(ulong steamId)
    {
        if (_helpTickHandlers.Remove(steamId, out var tickHandler))
        {
            RemoveListener(tickHandler);
        }

        if (_helpRefreshTimers.Remove(steamId, out var refreshTimer))
        {
            refreshTimer.Kill();
        }

        if (_helpTimers.Remove(steamId, out var timer))
        {
            timer.Kill();
        }

        _openHelpPanels.Remove(steamId);
    }

    private void ShowLevelUpPanel(CCSPlayerController player, string html, float durationSeconds)
    {
        if (!TryGetSteamId(player, out var steamId))
        {
            player.PrintToCenterHtml(html);
            return;
        }

        StopLevelUpPanel(steamId, false);
        _levelUpHtml[steamId] = html;
        var panelPlayer = player;
        panelPlayer.PrintToCenterHtml(html);

        CounterStrikeSharp.API.Core.Listeners.OnTick? tickHandler = null;
        tickHandler = () =>
        {
            if (!_levelUpHtml.ContainsKey(steamId))
            {
                if (tickHandler is not null)
                {
                    RemoveListener(tickHandler);
                }
                return;
            }

            if (!IsRealPlayer(panelPlayer))
            {
                if (tickHandler is not null)
                {
                    RemoveListener(tickHandler);
                }
                return;
            }

            if (_openHelpPanels.Contains(steamId) || MenuManager.GetActiveMenu(panelPlayer) is not null)
            {
                return;
            }

            panelPlayer.PrintToCenterHtml(_levelUpHtml[steamId]);
        };

        RegisterListener(tickHandler);
        _levelUpTickHandlers[steamId] = tickHandler;
        _levelUpCloseTimers[steamId] = AddTimer(durationSeconds, () => StopLevelUpPanel(steamId, true), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void ResetTransientUiState()
    {
        foreach (var tickHandler in _helpTickHandlers.Values)
        {
            RemoveListener(tickHandler);
        }

        foreach (var tickHandler in _levelUpTickHandlers.Values)
        {
            RemoveListener(tickHandler);
        }

        foreach (var timer in _helpRefreshTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _helpTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _levelUpRefreshTimers.Values)
        {
            timer.Kill();
        }

        foreach (var timer in _levelUpCloseTimers.Values)
        {
            timer.Kill();
        }

        _helpTickHandlers.Clear();
        _levelUpTickHandlers.Clear();
        _helpRefreshTimers.Clear();
        _helpTimers.Clear();
        _levelUpRefreshTimers.Clear();
        _levelUpCloseTimers.Clear();
        _levelUpHtml.Clear();
        _openHelpPanels.Clear();
        _lastHelpToggleAt.Clear();
    }

    private void StopLevelUpPanel(ulong steamId, bool clearCenterText)
    {
        _levelUpHtml.Remove(steamId);

        if (_levelUpTickHandlers.Remove(steamId, out var tickHandler))
        {
            RemoveListener(tickHandler);
        }

        if (_levelUpRefreshTimers.Remove(steamId, out var refreshTimer))
        {
            refreshTimer.Kill();
        }

        if (_levelUpCloseTimers.Remove(steamId, out var closeTimer))
        {
            closeTimer.Kill();
        }

        if (!clearCenterText)
        {
            return;
        }

        var player = Utilities.GetPlayerFromSteamId64(steamId);
        if (IsRealPlayer(player) && !_openHelpPanels.Contains(steamId) && MenuManager.GetActiveMenu(player!) is null)
        {
            player!.PrintToCenterHtml(" ");
        }
    }

    private void CloseActiveXPXMenu(CCSPlayerController player)
    {
        if (MenuManager.GetActiveMenu(player) is XPXNumberMenuInstance XPXMenu)
        {
            XPXMenu.Close();
            return;
        }

        MenuManager.CloseActiveMenu(player);
    }

    private string BuildLevelUpHtml(LevelState newState, IReadOnlyCollection<string> unlockedRewards, LevelReward? nextReward)
    {
        var unlockedText = unlockedRewards.Count == 0
            ? "No new reward on this level."
            : "Unlocked: " + string.Join(" | ", unlockedRewards);
        var nextRewardText = nextReward is null ? "All rewards unlocked" : DescribeReward(nextReward);

        return string.Join("<br>", new[]
        {
            "<b><font color='gold'>XPX Level Up</font></b>",
            $"<font color='white'>You leveled up to</font> <font color='gold'>{RenderShortLevelLabel(newState.Level)}</font><font color='white'>!</font>",
            $"<font color='deepskyblue'>{unlockedText}</font>",
            $"<font color='silver'>Next reward:</font> <font color='gold'>{nextRewardText}</font>"
        });
    }

    private string GetCurrentVisibleTag(PlayerProgress? progress)
    {
        if (progress is null)
        {
            return "NO TAG";
        }

        var tag = GetCurrentReward(_levelCurve.GetState(progress.TotalXp).Level)?.Tag;
        return string.IsNullOrWhiteSpace(tag) ? "NO TAG" : tag;
    }

    private string NormalizeStoredPlayerName(string? playerName)
    {
        var cleanName = (playerName ?? string.Empty).Trim();
        if (cleanName.Length == 0)
        {
            return "Player";
        }

        foreach (var rewardTag in Config.Rewards
                     .Select(reward => reward.Tag)
                     .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(static tag => tag.Length))
        {
            while (cleanName.StartsWith(rewardTag + " ", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName[(rewardTag.Length + 1)..].TrimStart();
            }

            if (string.Equals(cleanName, rewardTag, StringComparison.OrdinalIgnoreCase))
            {
                cleanName = "Player";
            }
        }

        return cleanName.Length == 0 ? "Player" : cleanName;
    }

    private static string BuildDisplayPlayerName(string cleanName, string tag)
    {
        var displayName = string.IsNullOrWhiteSpace(tag) ? cleanName : $"{tag} {cleanName}";
        return displayName.Length <= 127 ? displayName : displayName[..127];
    }

    private XPXNumberMenu CreateMenu(string title)
    {
        return new XPXNumberMenu(title, this)
        {
            TitleColor = "gold",
            EnabledColor = "white",
            DisabledColor = "gray",
            PrevPageColor = "deepskyblue",
            NextPageColor = "deepskyblue",
            CloseColor = "tomato"
        };
    }

    private bool HasPermission(CCSPlayerController? player, string permission)
    {
        return AdminManager.PlayerHasPermissions(player, PermissionRoot) ||
               AdminManager.PlayerHasPermissions(player, permission);
    }

    private void ApplyEquipmentState(CCSPlayerController player, PlayerProgress progress, LevelReward? reward)
    {
        if (_forcedLoadoutModeEnabled)
        {
            EquipPlayerForForcedLoadout(player, progress, reward);
            return;
        }

        GiveRewardKnife(player, reward);
    }

    private void GiveRewardKnife(CCSPlayerController player, LevelReward? reward)
    {
        if (!IsRealPlayer(player) || !player.PawnIsAlive || string.IsNullOrWhiteSpace(reward?.KnifeItem))
        {
            return;
        }

        player.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
        AddTimer(0.1f, () =>
        {
            if (IsRealPlayer(player) && player.PawnIsAlive)
            {
                player.GiveNamedItem(reward.KnifeItem);
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void EquipPlayerForForcedLoadout(CCSPlayerController player, PlayerProgress progress, LevelReward? reward = null)
    {
        if (!IsRealPlayer(player) || !player.PawnIsAlive)
        {
            return;
        }

        player.RemoveWeapons();
        AddTimer(0.08f, () =>
        {
            if (!IsRealPlayer(player) || !player.PawnIsAlive)
            {
                return;
            }

            switch (_forcedLoadout)
            {
                case ForcedLoadoutType.Rifle:
                    player.GiveNamedItem(player.Team == CsTeam.CounterTerrorist ? "weapon_m4a1_silencer" : "weapon_ak47");
                    break;
                case ForcedLoadoutType.Pistol:
                    player.GiveNamedItem(player.Team == CsTeam.CounterTerrorist ? "weapon_hkp2000" : "weapon_glock");
                    break;
                case ForcedLoadoutType.Knife:
                    player.GiveNamedItem(!string.IsNullOrWhiteSpace(reward?.KnifeItem) ? reward.KnifeItem : "weapon_knife");
                    break;
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void ReapplyForcedLoadoutToAlivePlayers()
    {
        if (!_forcedLoadoutModeEnabled)
        {
            return;
        }

        foreach (var player in GetHumanPlayers().Where(static player => player.PawnIsAlive))
        {
            var progress = EnsurePlayerProgress(player);
            if (progress is null)
            {
                continue;
            }

            var reward = GetCurrentReward(_levelCurve.GetState(progress.TotalXp).Level);
            EquipPlayerForForcedLoadout(player, progress, reward);
        }
    }

    private void ApplyDeathmatchModeCommands()
    {
        Server.ExecuteCommand("game_alias deathmatch");

        var (gameType, gameMode, mapGroup) = ResolveModeConVars("deathmatch");
        if (gameType is not null)
        {
            Server.ExecuteCommand($"game_type {gameType.Value}");
        }

        if (gameMode is not null)
        {
            Server.ExecuteCommand($"game_mode {gameMode.Value}");
        }

        if (!string.IsNullOrWhiteSpace(mapGroup))
        {
            Server.ExecuteCommand($"mapgroup {mapGroup}");
        }
    }

    private void ApplyForcedLoadoutServerRules()
    {
        Server.ExecuteCommand("mp_respawn_on_death_t 1");
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_teammates_are_enemies 1");
        Server.ExecuteCommand("mp_dm_teammode 0");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buy_during_immunity 0");
        Server.ExecuteCommand("mp_dm_bonus_length_min 0");
        Server.ExecuteCommand("mp_dm_bonus_length_max 0");
        Server.ExecuteCommand("mp_dm_bonus_percent 0");
    }

    private string GetForcedLoadoutStatusLabel()
    {
        return _forcedLoadoutModeEnabled ? GetForcedLoadoutLabel(_forcedLoadout) : "Off";
    }

    private static string GetForcedLoadoutLabel(ForcedLoadoutType loadout)
    {
        return loadout switch
        {
            ForcedLoadoutType.Rifle => "Rifles",
            ForcedLoadoutType.Pistol => "Pistols",
            ForcedLoadoutType.Knife => "Knives",
            _ => "Loadout"
        };
    }

    private void ReplyLoadoutCommandResult(CommandInfo? command, CCSPlayerController? actor, string message)
    {
        if (IsRealPlayer(actor))
        {
            Reply(actor!, message);
            return;
        }

        if (command is not null)
        {
            Reply(command, message);
        }
    }

    private void Reply(CCSPlayerController player, string message)
    {
        if (!IsRealPlayer(player))
        {
            return;
        }

        player.PrintToChat(FormatChat($"{Config.ChatPrefix} {message}"));
    }

    private void Reply(CommandInfo command, string message)
    {
        var formatted = command.CallingContext == CommandCallingContext.Chat
            ? FormatChat($"{Config.ChatPrefix} {message}")
            : StripColorTokens($"{Config.ChatPrefix} {message}");
        command.ReplyToCommand(formatted);
    }

    private void Broadcast(string message)
    {
        Server.PrintToChatAll(FormatChat($"{Config.ChatPrefix} {message}"));
    }

    private string FormatChat(string message)
    {
        var formatted = message;
        foreach (var (token, value) in ColorTokens)
        {
            formatted = formatted.Replace(token, value, StringComparison.OrdinalIgnoreCase);
        }

        return formatted;
    }

    private string StripColorTokens(string value)
    {
        var stripped = ColorTokenRegex.Replace(value, string.Empty);
        return new string(stripped.Where(static character => !char.IsControl(character) || character is '\r' or '\n' or '\t').ToArray());
    }
}
