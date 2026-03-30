using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace XPXLevels;

public sealed partial class XPXLevelsPlugin
{
    private readonly Dictionary<ulong, PlayerStats> _playerStats = new();
    private readonly Dictionary<ulong, Dictionary<string, WeaponStatProgress>> _weaponStatsBySteamId = new();
    private readonly Dictionary<ulong, Dictionary<string, PlayerMissionState>> _missionStatesBySteamId = new();
    private readonly Dictionary<ulong, HashSet<string>> _achievementKeysBySteamId = new();
    private readonly Dictionary<ulong, DateTimeOffset> _sessionJoinedUtc = new();
    private readonly Dictionary<ulong, int> _currentKillStreaks = new();
    private readonly Dictionary<ulong, int> _roundKillCounts = new();
    private bool _firstBloodAwardedThisRound;
    private PendingRoundModifier _queuedSpecialRound = new();
    private SpecialRoundType _activeSpecialRound;
    private WarmupEventType _warmupEvent = WarmupEventType.Default;

    [ConsoleCommand("css_stats", "Open your XPX stats overview")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnStatsCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenStatsMenu(player!);
        }
    }

    [ConsoleCommand("css_missions", "Open your XPX missions overview")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnMissionsCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenMissionsMenu(player!);
        }
    }

    [ConsoleCommand("css_achievements", "Open your XPX achievements overview")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnAchievementsCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenAchievementsMenu(player!);
        }
    }

    [ConsoleCommand("css_shop", "Open the XPX shop")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnShopCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenShopMenu(player!);
        }
    }

    public void OnCrateCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenCrateMenu(player!);
        }
    }

    [ConsoleCommand("css_wallet", "Show your XPX wallet")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnWalletCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (IsRealPlayer(player))
        {
            OpenWalletMenu(player!);
        }
    }

    [ConsoleCommand("css_kniferound", "Queue the next round as a knife-only round")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnKnifeRoundCommand(CCSPlayerController? caller, CommandInfo command)
    {
        QueueSpecialRound(SpecialRoundType.Knife, caller?.PlayerName ?? "Console");
        Reply(command, "{Gold}Queued a knife-only round.");
    }

    [ConsoleCommand("css_pistolround", "Queue the next round as a pistol round")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnPistolRoundCommand(CCSPlayerController? caller, CommandInfo command)
    {
        QueueSpecialRound(SpecialRoundType.Pistol, caller?.PlayerName ?? "Console");
        Reply(command, "{Gold}Queued a pistol round.");
    }

    [ConsoleCommand("css_warmupevent", "Set the active warmup event")]
    [RequiresPermissions(PermissionMap)]
    [CommandHelper(usage: "[default|pistols|knives|scouts|random]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnWarmupEventCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var requested = command.ArgCount > 1 ? command.GetArg(1) : "random";
        if (!TryParseWarmupEvent(requested, out var warmupEvent))
        {
            Reply(command, "{Red}Unknown warmup event. Use {White}default{Red}, {White}pistols{Red}, {White}knives{Red}, {White}scouts{Red}, or {White}random");
            return;
        }

        _warmupEvent = string.Equals(requested, "random", StringComparison.OrdinalIgnoreCase) ? PickWarmupEvent() : warmupEvent;
        Broadcast("{Gold}" + (caller?.PlayerName ?? "Console") + "{Default} set the warmup event to {White}" + DescribeWarmupEvent(_warmupEvent) + "{Default}.");
        ReapplyLoadoutsForAlivePlayers();
    }

    [GameEventHandler]
    public HookResult OnRoundStartFeatures(EventRoundStart @event, GameEventInfo info)
    {
        _firstBloodAwardedThisRound = false;
        _roundKillCounts.Clear();
        _activeSpecialRound = _queuedSpecialRound.Type;
        _queuedSpecialRound = new PendingRoundModifier();

        if (_activeSpecialRound != SpecialRoundType.None)
        {
            Broadcast("{Gold}Special round active: {White}" + DescribeSpecialRound(_activeSpecialRound) + "{Default}.");
            AddTimer(0.75f, ReapplyLoadoutsForAlivePlayers, TimerFlags.STOP_ON_MAPCHANGE);
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
    {
        if (IsWarmupActive() || !IsRealPlayer(@event.Userid))
        {
            return HookResult.Continue;
        }

        var mvp = @event.Userid!;
        var stats = EnsureFeatureStateLoaded(mvp);
        if (stats is null)
        {
            return HookResult.Continue;
        }

        stats.Mvps++;
        SaveFeatureState(stats.SteamId);
        UpdateMissionProgress(mvp, MissionObjective.Mvps, 1);
        EvaluateAchievements(mvp);
        AwardBonus(mvp, Config.MvpXp, Config.MvpCredits, "round MVP");
        return HookResult.Continue;
    }

    private PlayerStats? EnsureFeatureStateLoaded(CCSPlayerController player)
    {
        if (_repository is null || !TryGetSteamId(player, out var steamId))
        {
            return null;
        }

        if (!_playerStats.TryGetValue(steamId, out var stats))
        {
            stats = _repository.GetOrCreateStats(steamId);
            _playerStats[steamId] = stats;
        }

        if (!_weaponStatsBySteamId.ContainsKey(steamId))
        {
            var weaponStats = new Dictionary<string, WeaponStatProgress>(StringComparer.OrdinalIgnoreCase);
            foreach (var storedStat in _repository.GetWeaponStats(steamId))
            {
                var weaponKey = NormalizeWeaponName(storedStat.WeaponName);
                if (string.IsNullOrWhiteSpace(weaponKey))
                {
                    continue;
                }

                if (!weaponStats.TryGetValue(weaponKey, out var progress))
                {
                    progress = new WeaponStatProgress
                    {
                        SteamId = steamId,
                        WeaponName = weaponKey
                    };
                    weaponStats[weaponKey] = progress;
                }

                progress.Kills += storedStat.Kills;
                progress.Headshots += storedStat.Headshots;
            }

            _weaponStatsBySteamId[steamId] = weaponStats;
        }

        if (!_missionStatesBySteamId.ContainsKey(steamId))
        {
            var missionStates = new Dictionary<string, PlayerMissionState>(StringComparer.OrdinalIgnoreCase);
            foreach (var storedState in _repository.GetMissionStates(steamId))
            {
                if (string.IsNullOrWhiteSpace(storedState.MissionKey) || string.IsNullOrWhiteSpace(storedState.PeriodKey))
                {
                    continue;
                }

                var missionKey = BuildMissionLookupKey(storedState.MissionKey, storedState.PeriodKey);
                if (!missionStates.TryGetValue(missionKey, out var currentBest) || IsBetterMissionState(storedState, currentBest))
                {
                    missionStates[missionKey] = storedState;
                }
            }

            _missionStatesBySteamId[steamId] = missionStates;
        }

        if (!_achievementKeysBySteamId.ContainsKey(steamId))
        {
            _achievementKeysBySteamId[steamId] = _repository.GetAchievements(steamId)
                .Select(static state => state.AchievementKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        _sessionJoinedUtc.TryAdd(steamId, DateTimeOffset.UtcNow);
        return stats;
    }

    private void SaveFeatureState(ulong steamId)
    {
        if (_repository is null)
        {
            return;
        }

        UpdateSessionPlaytime(steamId);
        var player = Utilities.GetPlayerFromSteamId64(steamId);
        if (IsRealPlayer(player))
        {
            SyncPassiveMissionProgress(player!);
        }

        if (_playerStats.TryGetValue(steamId, out var stats))
        {
            _repository.SaveStats(stats);
        }

        if (_weaponStatsBySteamId.TryGetValue(steamId, out var weaponStats))
        {
            _repository.SaveWeaponStats(steamId, weaponStats.Values);
        }

        if (_missionStatesBySteamId.TryGetValue(steamId, out var missionStates))
        {
            foreach (var missionState in missionStates.Values)
            {
                _repository.SaveMissionState(missionState);
            }
        }
    }

    private void SaveAllFeatureState()
    {
        foreach (var steamId in _players.Keys.ToList())
        {
            SaveFeatureState(steamId);
        }
    }

    private void ResetFeatureRoundState()
    {
        _currentKillStreaks.Clear();
        _roundKillCounts.Clear();
        _firstBloodAwardedThisRound = false;
        _activeSpecialRound = SpecialRoundType.None;
        _queuedSpecialRound = new PendingRoundModifier();
        _warmupEvent = PickWarmupEvent();
    }

    private void UnloadFeatureStateForPlayer(ulong steamId)
    {
        SaveFeatureState(steamId);
        _playerStats.Remove(steamId);
        _weaponStatsBySteamId.Remove(steamId);
        _missionStatesBySteamId.Remove(steamId);
        _achievementKeysBySteamId.Remove(steamId);
        _sessionJoinedUtc.Remove(steamId);
        _currentKillStreaks.Remove(steamId);
        _roundKillCounts.Remove(steamId);
    }

    private void UpdateSessionPlaytime(ulong steamId)
    {
        if (!_sessionJoinedUtc.TryGetValue(steamId, out var startedAt) || !_playerStats.TryGetValue(steamId, out var stats))
        {
            return;
        }

        var elapsed = (long)Math.Floor((DateTimeOffset.UtcNow - startedAt).TotalSeconds);
        if (elapsed <= 0)
        {
            return;
        }

        stats.PlaytimeSeconds += elapsed;
        _sessionJoinedUtc[steamId] = DateTimeOffset.UtcNow;
    }

    private long GetDisplayPlaytimeSeconds(ulong steamId, PlayerStats stats)
    {
        if (!_sessionJoinedUtc.TryGetValue(steamId, out var startedAt))
        {
            return stats.PlaytimeSeconds;
        }

        return stats.PlaytimeSeconds + Math.Max(0L, (long)Math.Floor((DateTimeOffset.UtcNow - startedAt).TotalSeconds));
    }

    private void HandleKillFeatureProgress(CCSPlayerController killer, CCSPlayerController victim, string weaponName, bool headshot, bool knifeKill)
    {
        var killerStats = EnsureFeatureStateLoaded(killer);
        if (killerStats is null || !TryGetSteamId(killer, out var killerSteamId))
        {
            return;
        }

        killerStats.Kills++;
        if (headshot)
        {
            killerStats.Headshots++;
        }

        if (knifeKill)
        {
            killerStats.KnifeKills++;
        }

        if (IsRealPlayer(victim))
        {
            var victimStats = EnsureFeatureStateLoaded(victim);
            if (victimStats is not null && TryGetSteamId(victim, out var victimSteamId))
            {
                victimStats.Deaths++;
                _currentKillStreaks[victimSteamId] = 0;
                SaveFeatureState(victimSteamId);
            }
        }

        var normalizedWeapon = NormalizeWeaponName(weaponName);
        var weaponStats = GetOrCreateWeaponStats(killerSteamId, normalizedWeapon);
        weaponStats.Kills++;
        if (headshot)
        {
            weaponStats.Headshots++;
        }

        _currentKillStreaks.TryGetValue(killerSteamId, out var currentStreak);
        currentStreak++;
        _currentKillStreaks[killerSteamId] = currentStreak;
        killerStats.BestKillStreak = Math.Max(killerStats.BestKillStreak, currentStreak);

        _roundKillCounts.TryGetValue(killerSteamId, out var roundKillCount);
        roundKillCount++;
        _roundKillCounts[killerSteamId] = roundKillCount;

        var killCredits = victim.IsBot
            ? (int)Math.Round(GetBaseKillCreditsForCurrentMode() * Config.BotXpMultiplier, MidpointRounding.AwayFromZero)
            : GetBaseKillCreditsForCurrentMode();
        if (killCredits > 0)
        {
            AdjustCredits(killer, killCredits, "kill reward", false);
        }

        UpdateMissionProgress(killer, MissionObjective.Kills, 1);
        if (headshot)
        {
            UpdateMissionProgress(killer, MissionObjective.Headshots, 1);
        }

        if (knifeKill)
        {
            UpdateMissionProgress(killer, MissionObjective.KnifeKills, 1);
        }

        if (!_firstBloodAwardedThisRound)
        {
            _firstBloodAwardedThisRound = true;
            killerStats.FirstBloods++;
            UpdateMissionProgress(killer, MissionObjective.FirstBloods, 1);
            AwardBonus(killer, Config.FirstBloodXp, Config.FirstBloodCredits, "first blood");
        }

        var multikillBonus = Config.MultikillBonuses.FirstOrDefault(bonus => bonus.Kills == roundKillCount);
        if (multikillBonus is not null)
        {
            killerStats.MultiKills++;
            AwardBonus(killer, multikillBonus.RewardXp, multikillBonus.RewardCredits, multikillBonus.Label);
        }

        var streakBonus = Config.KillstreakBonuses.FirstOrDefault(bonus => bonus.Threshold == currentStreak);
        if (streakBonus is not null)
        {
            AwardBonus(killer, streakBonus.RewardXp, streakBonus.RewardCredits, streakBonus.Label);
        }

        EvaluateAchievements(killer);
        SaveFeatureState(killerSteamId);
    }

    private void HandleAssistFeatureProgress(CCSPlayerController assister)
    {
        var stats = EnsureFeatureStateLoaded(assister);
        if (stats is null)
        {
            return;
        }

        stats.Assists++;
        UpdateMissionProgress(assister, MissionObjective.Assists, 1);
        EvaluateAchievements(assister);
        AwardBonus(assister, Config.AssistXp, Config.AssistCredits, "assist");

        if (TryGetSteamId(assister, out var steamId))
        {
            SaveFeatureState(steamId);
        }
    }

    private void HandleRoundWinFeatureProgress(CsTeam winningTeam)
    {
        foreach (var player in GetHumanPlayers().Where(player => player.Team == winningTeam))
        {
            var stats = EnsureFeatureStateLoaded(player);
            if (stats is null)
            {
                continue;
            }

            stats.RoundWins++;
            UpdateMissionProgress(player, MissionObjective.Wins, 1);
            EvaluateAchievements(player);

            if (Config.RoundWinCredits > 0)
            {
                AdjustCredits(player, Config.RoundWinCredits, "round win", false);
            }

            if (TryGetSteamId(player, out var steamId))
            {
                SaveFeatureState(steamId);
            }
        }

        var aliveWinners = GetHumanPlayers().Where(player => player.Team == winningTeam && player.PawnIsAlive).ToList();
        var totalWinners = GetHumanPlayers().Count(player => player.Team == winningTeam);
        if (aliveWinners.Count == 1 && totalWinners >= 2)
        {
            var clutchPlayer = aliveWinners[0];
            var stats = EnsureFeatureStateLoaded(clutchPlayer);
            if (stats is not null)
            {
                stats.ClutchWins++;
                UpdateMissionProgress(clutchPlayer, MissionObjective.ClutchWins, 1);
                EvaluateAchievements(clutchPlayer);
                AwardBonus(clutchPlayer, Config.ClutchXp, Config.ClutchCredits, "clutch win");
                if (TryGetSteamId(clutchPlayer, out var steamId))
                {
                    SaveFeatureState(steamId);
                }
            }
        }
    }

    private void HandleBombFeatureProgress(CCSPlayerController player, MissionObjective objective)
    {
        var stats = EnsureFeatureStateLoaded(player);
        if (stats is null)
        {
            return;
        }

        if (objective == MissionObjective.BombPlants)
        {
            stats.BombPlants++;
        }
        else if (objective == MissionObjective.BombDefuses)
        {
            stats.BombDefuses++;
        }

        UpdateMissionProgress(player, objective, 1);
        UpdateMissionProgress(player, MissionObjective.BombObjectives, 1);
        EvaluateAchievements(player);

        if (TryGetSteamId(player, out var steamId))
        {
            SaveFeatureState(steamId);
        }
    }

    private void AwardBonus(CCSPlayerController player, int xp, int credits, string label)
    {
        if (xp > 0)
        {
            AdjustXp(player, xp, label, false);
        }

        if (credits > 0)
        {
            AdjustCredits(player, credits, label, false);
        }

        if (xp <= 0 && credits <= 0)
        {
            return;
        }

        Reply(player, "{Green}Bonus unlocked: {White}" + label + "{Green} +" + xp + " XP +" + credits + " " + Config.CurrencyName);
    }

    private bool AdjustCredits(CCSPlayerController player, int delta, string reason, bool showMessage)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null || delta == 0 || _repository is null)
        {
            return false;
        }

        var newCredits = Math.Max(0, progress.Credits + delta);
        var actualDelta = newCredits - progress.Credits;
        if (actualDelta == 0)
        {
            return false;
        }

        progress.Credits = newCredits;
        _repository.SavePlayer(progress);

        if (showMessage)
        {
            var verb = actualDelta > 0 ? "received" : "spent";
            Reply(player,
                (actualDelta > 0 ? "{Green}" : "{Red}") + "You " + verb + " {White}" + Math.Abs(actualDelta).ToString("N0", CultureInfo.InvariantCulture) +
                "{Default} " + Config.CurrencyName + " for " + reason + ".");
        }

        return true;
    }

    private void UpdateMissionProgress(CCSPlayerController player, MissionObjective objective, int amount)
    {
        if (!TryGetSteamId(player, out var steamId) || amount <= 0)
        {
            return;
        }

        foreach (var mission in GetActiveMissionDefinitions().Where(mission => mission.Objective == objective))
        {
            var missionState = GetOrCreateMissionState(steamId, mission);
            if (missionState.CompletedUtc is not null)
            {
                continue;
            }

            missionState.Progress = Math.Min(mission.Goal, missionState.Progress + amount);
            if (missionState.Progress >= mission.Goal)
            {
                missionState.CompletedUtc = DateTimeOffset.UtcNow;
                if (_playerStats.TryGetValue(steamId, out var stats))
                {
                    stats.MissionsCompleted++;
                }

                _repository?.SaveMissionState(missionState);
                AwardMissionCompletion(player, mission);
            }
            else
            {
                _repository?.SaveMissionState(missionState);
            }
        }
    }

    private void AwardMissionCompletion(CCSPlayerController player, MissionDefinition mission)
    {
        if (mission.RewardXp > 0)
        {
            AdjustXp(player, mission.RewardXp, mission.Title, false);
        }

        if (mission.RewardCredits > 0)
        {
            AdjustCredits(player, mission.RewardCredits, mission.Title, false);
        }

        Reply(player,
            "{Gold}Mission complete: {White}" + mission.Title +
            "{Gold}. Rewards: {White}" + mission.RewardXp + "{Gold} XP, {White}" + mission.RewardCredits + "{Gold} " + Config.CurrencyName + ".");
    }

    private void EvaluateAchievements(CCSPlayerController player)
    {
        if (!TryGetSteamId(player, out var steamId))
        {
            return;
        }

        var stats = EnsureFeatureStateLoaded(player);
        if (stats is null || _repository is null)
        {
            return;
        }

        if (!_achievementKeysBySteamId.TryGetValue(steamId, out var unlockedKeys))
        {
            unlockedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _achievementKeysBySteamId[steamId] = unlockedKeys;
        }

        foreach (var achievement in Config.Achievements)
        {
            if (unlockedKeys.Contains(achievement.Key) || GetObjectiveValue(stats, achievement.Objective) < achievement.Goal)
            {
                continue;
            }

            unlockedKeys.Add(achievement.Key);
            stats.AchievementsUnlocked++;
            _repository.SaveAchievement(new PlayerAchievementState
            {
                SteamId = steamId,
                AchievementKey = achievement.Key,
                UnlockedUtc = DateTimeOffset.UtcNow
            });

            if (achievement.RewardCredits > 0)
            {
                AdjustCredits(player, achievement.RewardCredits, achievement.Title, false);
            }

            Reply(player,
                "{Gold}Achievement unlocked: {White}" + achievement.Title +
                "{Gold} [" + achievement.Badge + "] +" + achievement.RewardCredits + " " + Config.CurrencyName);
        }
    }

    private int GetObjectiveValue(PlayerStats stats, MissionObjective objective)
    {
        return objective switch
        {
            MissionObjective.Kills => stats.Kills,
            MissionObjective.Headshots => stats.Headshots,
            MissionObjective.Wins => stats.RoundWins,
            MissionObjective.KnifeKills => stats.KnifeKills,
            MissionObjective.Assists => stats.Assists,
            MissionObjective.Mvps => stats.Mvps,
            MissionObjective.BombPlants => stats.BombPlants,
            MissionObjective.BombDefuses => stats.BombDefuses,
            MissionObjective.BombObjectives => stats.BombPlants + stats.BombDefuses,
            MissionObjective.FirstBloods => stats.FirstBloods,
            MissionObjective.ClutchWins => stats.ClutchWins,
            MissionObjective.PlayMinutes => (int)(stats.PlaytimeSeconds / 60L),
            MissionObjective.CratesOpened => stats.CratesOpened,
            _ => 0
        };
    }

    private IEnumerable<MissionDefinition> GetActiveMissionDefinitions()
    {
        return Config.DailyMissions.Concat(Config.WeeklyMissions);
    }

    private PlayerMissionState GetOrCreateMissionState(ulong steamId, MissionDefinition mission)
    {
        var periodKey = GetMissionPeriodKey(mission.Scope);
        var lookupKey = BuildMissionLookupKey(mission.Key, periodKey);

        if (!_missionStatesBySteamId.TryGetValue(steamId, out var missionStates))
        {
            missionStates = new Dictionary<string, PlayerMissionState>(StringComparer.OrdinalIgnoreCase);
            _missionStatesBySteamId[steamId] = missionStates;
        }

        if (missionStates.TryGetValue(lookupKey, out var missionState))
        {
            return missionState;
        }

        missionState = new PlayerMissionState
        {
            SteamId = steamId,
            MissionKey = mission.Key,
            PeriodKey = periodKey,
            Progress = 0
        };
        missionStates[lookupKey] = missionState;
        return missionState;
    }

    private static string BuildMissionLookupKey(string missionKey, string periodKey)
    {
        return missionKey + "::" + periodKey;
    }

    private static bool IsBetterMissionState(PlayerMissionState candidate, PlayerMissionState current)
    {
        var candidateCompleted = candidate.CompletedUtc ?? DateTimeOffset.MinValue;
        var currentCompleted = current.CompletedUtc ?? DateTimeOffset.MinValue;
        if (candidateCompleted != currentCompleted)
        {
            return candidateCompleted > currentCompleted;
        }

        if (candidate.Progress != current.Progress)
        {
            return candidate.Progress > current.Progress;
        }

        return false;
    }

    private static string GetMissionPeriodKey(string scope)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(scope, "weekly", StringComparison.OrdinalIgnoreCase))
        {
            var week = ISOWeek.GetWeekOfYear(now.UtcDateTime);
            return $"{now.Year}-W{week:00}";
        }

        return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private WeaponStatProgress GetOrCreateWeaponStats(ulong steamId, string weaponName)
    {
        if (!_weaponStatsBySteamId.TryGetValue(steamId, out var weaponStats))
        {
            weaponStats = new Dictionary<string, WeaponStatProgress>(StringComparer.OrdinalIgnoreCase);
            _weaponStatsBySteamId[steamId] = weaponStats;
        }

        if (weaponStats.TryGetValue(weaponName, out var stat))
        {
            return stat;
        }

        stat = new WeaponStatProgress
        {
            SteamId = steamId,
            WeaponName = weaponName
        };
        weaponStats[weaponName] = stat;
        return stat;
    }

    private int GetBaseKillCreditsForCurrentMode()
    {
        return IsFastXpMode(GetCurrentGameModeAlias()) ? Config.FastModeKillCredits : Config.CasualCompetitiveKillCredits;
    }

    private void SyncPassiveMissionProgress(CCSPlayerController player)
    {
        if (!TryGetSteamId(player, out var steamId) || !_playerStats.TryGetValue(steamId, out var stats))
        {
            return;
        }

        UpdateSessionPlaytime(steamId);
        foreach (var mission in GetActiveMissionDefinitions().Where(static mission => mission.Objective == MissionObjective.PlayMinutes))
        {
            var missionState = GetOrCreateMissionState(steamId, mission);
            if (missionState.CompletedUtc is not null)
            {
                continue;
            }

            var targetProgress = Math.Min(mission.Goal, (int)(stats.PlaytimeSeconds / 60L));
            if (targetProgress <= missionState.Progress)
            {
                continue;
            }

            missionState.Progress = targetProgress;
            if (missionState.Progress >= mission.Goal)
            {
                missionState.CompletedUtc = DateTimeOffset.UtcNow;
                stats.MissionsCompleted++;
                _repository?.SaveMissionState(missionState);
                AwardMissionCompletion(player, mission);
            }
            else
            {
                _repository?.SaveMissionState(missionState);
            }
        }
    }

    private void OpenStatsMenu(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        var stats = EnsureFeatureStateLoaded(player);
        if (progress is null || stats is null || !TryGetSteamId(player, out var steamId))
        {
            return;
        }

        SyncPassiveMissionProgress(player);
        var kd = stats.Deaths == 0
            ? stats.Kills.ToString("0.00", CultureInfo.InvariantCulture)
            : ((double)stats.Kills / stats.Deaths).ToString("0.00", CultureInfo.InvariantCulture);
        var hsRate = stats.Kills == 0 ? "0.0%" : ((double)stats.Headshots / stats.Kills * 100d).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        var favoriteWeapon = GetFavoriteWeapon(steamId);
        var playtime = FormatDuration(GetDisplayPlaytimeSeconds(steamId, stats));

        OpenReadOnlyMenu(player,
            "Stats",
            new[]
            {
                $"K/D: {kd} | HS%: {hsRate}",
                $"Kills: {stats.Kills} | Deaths: {stats.Deaths} | Assists: {stats.Assists}",
                $"Wins: {stats.RoundWins} | MVPs: {stats.Mvps} | Clutches: {stats.ClutchWins}",
                $"Best streak: {stats.BestKillStreak} | Multikills: {stats.MultiKills}",
                $"Favorite weapon: {favoriteWeapon}",
                $"Playtime: {playtime}",
                $"{Config.CurrencyName}: {progress.Credits} | Crates: {progress.CrateTokens}"
            });
    }

    private void OpenMissionsMenu(CCSPlayerController player)
    {
        if (!TryGetSteamId(player, out var steamId))
        {
            return;
        }

        EnsureFeatureStateLoaded(player);
        SyncPassiveMissionProgress(player);
        var lines = GetActiveMissionDefinitions()
            .Select(mission =>
            {
                var state = GetOrCreateMissionState(steamId, mission);
                var progress = Math.Min(mission.Goal, state.Progress);
                var status = state.CompletedUtc is null ? $"{progress}/{mission.Goal}" : "Done";
                var scope = string.Equals(mission.Scope, "weekly", StringComparison.OrdinalIgnoreCase) ? "W" : "D";
                return $"[{scope}] {mission.Title}: {status} | +{mission.RewardXp} XP +{mission.RewardCredits} {Config.CurrencyName}";
            })
            .ToList();

        OpenReadOnlyMenu(player, "Missions", lines);
    }

    private void OpenAchievementsMenu(CCSPlayerController player)
    {
        var stats = EnsureFeatureStateLoaded(player);
        if (stats is null || !TryGetSteamId(player, out var steamId))
        {
            return;
        }

        var unlocked = _achievementKeysBySteamId.TryGetValue(steamId, out var unlockedKeys)
            ? unlockedKeys
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = Config.Achievements
            .Select(achievement =>
            {
                var current = GetObjectiveValue(stats, achievement.Objective);
                var status = unlocked.Contains(achievement.Key) ? "Unlocked" : $"{Math.Min(current, achievement.Goal)}/{achievement.Goal}";
                return $"{achievement.Badge}: {achievement.Title} | {status}";
            })
            .ToList();

        OpenReadOnlyMenu(player, "Achievements", lines);
    }

    private void OpenWalletMenu(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        var stats = EnsureFeatureStateLoaded(player);
        if (progress is null || stats is null)
        {
            return;
        }

        SyncPassiveMissionProgress(player);
        OpenReadOnlyMenu(player,
            "Wallet",
            new[]
            {
                $"{Config.CurrencyName}: {progress.Credits}",
                $"Crate tokens: {progress.CrateTokens}",
                $"Crates opened: {stats.CratesOpened}",
                $"Missions completed: {stats.MissionsCompleted}",
                $"Achievements unlocked: {stats.AchievementsUnlocked}"
            });
    }

    private void OpenShopMenu(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        var menu = CreateMenu($"Shop | {progress.Credits} {Config.CurrencyName}");
        foreach (var item in Config.ShopItems)
        {
            var selectedItem = item;
            var affordable = progress.Credits >= item.CostCredits;
            menu.AddMenuOption($"{selectedItem.Name} ({selectedItem.CostCredits} {Config.CurrencyName})", (_, _) =>
            {
                PurchaseShopItem(player, selectedItem);
            }, disabled: !affordable);
        }

        menu.AddMenuOption("Crates", (target, _) => OpenCrateMenu(target));
        menu.AddMenuOption("Back to me", (target, _) => OpenMeMenu(target));
        OpenXPXMenu(player, menu);
    }

    private void OpenSpecialRoundsMenu(CCSPlayerController player)
    {
        var menu = CreateMenu("Special Rounds");
        menu.AddMenuOption("Queue knife round", (_, _) => QueueSpecialRound(SpecialRoundType.Knife, player.PlayerName));
        menu.AddMenuOption("Queue pistol round", (_, _) => QueueSpecialRound(SpecialRoundType.Pistol, player.PlayerName));
        menu.AddMenuOption("Random warmup event", (_, _) =>
        {
            _warmupEvent = PickWarmupEvent();
            Broadcast("{Gold}" + player.PlayerName + "{Default} rolled warmup event {White}" + DescribeWarmupEvent(_warmupEvent) + "{Default}.");
            ReapplyLoadoutsForAlivePlayers();
        });
        menu.AddMenuOption("Pistols warmup", (_, _) =>
        {
            _warmupEvent = WarmupEventType.PistolsOnly;
            ReapplyLoadoutsForAlivePlayers();
        });
        menu.AddMenuOption("Knives warmup", (_, _) =>
        {
            _warmupEvent = WarmupEventType.KnivesOnly;
            ReapplyLoadoutsForAlivePlayers();
        });
        menu.AddMenuOption("Scouts warmup", (_, _) =>
        {
            _warmupEvent = WarmupEventType.ScoutsOnly;
            ReapplyLoadoutsForAlivePlayers();
        });
        menu.AddMenuOption("Back to admin", (target, _) => OpenAdminMenu(target));
        OpenXPXMenu(player, menu);
    }

    private void OpenCrateMenu(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        var crate = Config.Crates.FirstOrDefault();
        if (progress is null || crate is null)
        {
            return;
        }

        var menu = CreateMenu($"Crates | {progress.CrateTokens} tokens");
        menu.AddMenuOption($"Open {crate.Name}", (_, _) => OpenCrate(player), disabled: progress.CrateTokens <= 0);
        menu.AddMenuOption($"Buy token ({crate.CostCredits} {Config.CurrencyName})", (_, _) =>
        {
            PurchaseCrateToken(player, crate);
        }, disabled: progress.Credits < crate.CostCredits);
        menu.AddMenuOption("Back to shop", (target, _) => OpenShopMenu(target));
        menu.AddMenuOption("Back to me", (target, _) => OpenMeMenu(target));
        OpenXPXMenu(player, menu);
    }

    private void PurchaseShopItem(CCSPlayerController player, ShopItemDefinition item)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null)
        {
            return;
        }

        if (progress.Credits < item.CostCredits)
        {
            Reply(player, "{Red}You do not have enough " + Config.CurrencyName + " for that purchase.");
            OpenShopMenu(player);
            return;
        }

        AdjustCredits(player, -item.CostCredits, item.Name, false);
        ApplyShopReward(player, item.Name, item.RewardType, item.RewardAmount);
        Reply(player, "{Green}Purchased {White}" + item.Name + "{Green}.");
        OpenShopMenu(player);
    }

    private void PurchaseCrateToken(CCSPlayerController player, CrateDefinition crate)
    {
        var progress = EnsurePlayerProgress(player);
        if (progress is null || progress.Credits < crate.CostCredits)
        {
            Reply(player, "{Red}You do not have enough " + Config.CurrencyName + " for a crate token.");
            OpenCrateMenu(player);
            return;
        }

        AdjustCredits(player, -crate.CostCredits, crate.Name + " token", false);
        progress = EnsurePlayerProgress(player);
        if (progress is not null)
        {
            progress.CrateTokens++;
            _repository?.SavePlayer(progress);
        }

        Reply(player, "{Green}Purchased one {White}" + crate.Name + "{Green} token.");
        OpenCrateMenu(player);
    }

    private void OpenCrate(CCSPlayerController player)
    {
        var progress = EnsurePlayerProgress(player);
        var crate = Config.Crates.FirstOrDefault();
        var stats = EnsureFeatureStateLoaded(player);
        if (progress is null || crate is null || stats is null)
        {
            return;
        }

        if (progress.CrateTokens <= 0)
        {
            Reply(player, "{Red}You do not have any crate tokens to open.");
            OpenCrateMenu(player);
            return;
        }

        var reward = RollCrateReward(crate);
        progress.CrateTokens--;
        _repository?.SavePlayer(progress);
        ApplyShopReward(player, reward.Label, reward.RewardType, reward.RewardAmount);

        stats.CratesOpened++;
        UpdateMissionProgress(player, MissionObjective.CratesOpened, 1);
        if (TryGetSteamId(player, out var steamId))
        {
            SaveFeatureState(steamId);
        }

        OpenReadOnlyMenu(player,
            "Crate Opened",
            new[]
            {
                $"Case: {crate.Name}",
                $"Reward: {reward.Label}",
                $"Remaining tokens: {progress.CrateTokens}"
            });
    }

    private void ApplyShopReward(CCSPlayerController player, string label, ShopRewardType rewardType, int rewardAmount)
    {
        switch (rewardType)
        {
            case ShopRewardType.Xp:
                AdjustXp(player, rewardAmount, label, false);
                Reply(player, "{Green}Reward: {White}" + rewardAmount + "{Green} XP.");
                break;
            case ShopRewardType.Credits:
                AdjustCredits(player, rewardAmount, label, false);
                Reply(player, "{Green}Reward: {White}" + rewardAmount + "{Green} " + Config.CurrencyName + ".");
                break;
            case ShopRewardType.CrateToken:
            {
                var progress = EnsurePlayerProgress(player);
                if (progress is not null)
                {
                    progress.CrateTokens += rewardAmount;
                    _repository?.SavePlayer(progress);
                    Reply(player, "{Green}Reward: {White}" + rewardAmount + "{Green} crate token(s).");
                }

                break;
            }
        }
    }

    private CrateRewardDefinition RollCrateReward(CrateDefinition crate)
    {
        var totalWeight = Math.Max(1, crate.Rewards.Sum(reward => Math.Max(1, reward.Weight)));
        var roll = _random.Next(1, totalWeight + 1);
        var current = 0;
        foreach (var reward in crate.Rewards)
        {
            current += Math.Max(1, reward.Weight);
            if (roll <= current)
            {
                return reward;
            }
        }

        return crate.Rewards.Last();
    }

    private string GetFavoriteWeapon(ulong steamId)
    {
        if (!_weaponStatsBySteamId.TryGetValue(steamId, out var weaponStats) || weaponStats.Count == 0)
        {
            return "None yet";
        }

        var favorite = weaponStats.Values
            .OrderByDescending(stat => stat.Kills)
            .ThenBy(stat => stat.WeaponName, StringComparer.OrdinalIgnoreCase)
            .First();

        return favorite.WeaponName + $" ({favorite.Kills})";
    }

    private string NormalizeWeaponName(string? weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return "unknown";
        }

        var normalized = weaponName.Trim().Replace("weapon_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace('_', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private static string FormatDuration(long seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }

        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    private void QueueSpecialRound(SpecialRoundType type, string actorName)
    {
        _queuedSpecialRound = new PendingRoundModifier
        {
            Type = type,
            SetBy = actorName
        };

        Broadcast("{Gold}" + actorName + "{Default} queued a {White}" + DescribeSpecialRound(type) + "{Default}.");
    }

    private static string DescribeSpecialRound(SpecialRoundType type)
    {
        return type switch
        {
            SpecialRoundType.Knife => "knife round",
            SpecialRoundType.Pistol => "pistol round",
            _ => "normal round"
        };
    }

    private static string DescribeWarmupEvent(WarmupEventType type)
    {
        return type switch
        {
            WarmupEventType.PistolsOnly => "Pistols Only",
            WarmupEventType.KnivesOnly => "Knives Only",
            WarmupEventType.ScoutsOnly => "Scouts Only",
            _ => "Default Warmup"
        };
    }

    private WarmupEventType PickWarmupEvent()
    {
        var events = new[] { WarmupEventType.PistolsOnly, WarmupEventType.KnivesOnly, WarmupEventType.ScoutsOnly };
        return events[_random.Next(events.Length)];
    }

    private static bool TryParseWarmupEvent(string value, out WarmupEventType warmupEvent)
    {
        var normalized = value.Trim().ToLowerInvariant();
        warmupEvent = normalized switch
        {
            "default" => WarmupEventType.Default,
            "pistol" or "pistols" => WarmupEventType.PistolsOnly,
            "knife" or "knives" => WarmupEventType.KnivesOnly,
            "scout" or "scouts" => WarmupEventType.ScoutsOnly,
            "random" => WarmupEventType.Default,
            _ => WarmupEventType.Default
        };

        return normalized is "default" or "pistol" or "pistols" or "knife" or "knives" or "scout" or "scouts" or "random";
    }

    private void ReapplyLoadoutsForAlivePlayers()
    {
        foreach (var player in GetHumanPlayers().Where(static player => player.PawnIsAlive))
        {
            ApplySpecialLoadout(player);
        }
    }

    private void ApplySpecialLoadout(CCSPlayerController player)
    {
        if (!IsRealPlayer(player) || !player.PawnIsAlive)
        {
            return;
        }

        if (IsWarmupActive())
        {
            switch (_warmupEvent)
            {
                case WarmupEventType.PistolsOnly:
                    EquipPistolLoadout(player);
                    return;
                case WarmupEventType.KnivesOnly:
                    EquipKnifeLoadout(player);
                    return;
                case WarmupEventType.ScoutsOnly:
                    EquipScoutLoadout(player);
                    return;
            }
        }

        switch (_activeSpecialRound)
        {
            case SpecialRoundType.Knife:
                EquipKnifeLoadout(player);
                break;
            case SpecialRoundType.Pistol:
                EquipPistolLoadout(player);
                break;
        }
    }

    private void EquipKnifeLoadout(CCSPlayerController player)
    {
        player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
    }

    private void EquipPistolLoadout(CCSPlayerController player)
    {
        player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem(player.Team == CsTeam.CounterTerrorist ? "weapon_hkp2000" : "weapon_glock");
    }

    private void EquipScoutLoadout(CCSPlayerController player)
    {
        player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("weapon_ssg08");
    }
}
