namespace XPXLevels;

public sealed class PlayerProgress
{
    public ulong SteamId { get; init; }
    public string PlayerName { get; set; } = string.Empty;
    public long TotalXp { get; set; }
    public DateTimeOffset LastGambleAttemptUtc { get; set; } = DateTimeOffset.MinValue;
}

public readonly record struct LevelState(int Level, long TotalXp, long XpIntoLevel, long XpNeededForNextLevel);

public sealed record TopPlayerEntry(int Rank, ulong SteamId, string PlayerName, long TotalXp);

public sealed record ServerMapOption(string Key, string DisplayName, string CommandTarget, bool IsWorkshop);

public sealed class TransitionSnapshot
{
    public DateTimeOffset CreatedUtc { get; set; }
    public List<TransitionSnapshotEntry> Players { get; set; } = [];
}

public sealed class TransitionSnapshotEntry
{
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public long TotalXp { get; set; }
}

public sealed class MapVoteSession
{
    public MapVoteSession(IReadOnlyList<ServerMapOption> options, string startedBy, DateTimeOffset endsAtUtc)
    {
        Options = options;
        StartedBy = startedBy;
        EndsAtUtc = endsAtUtc;
    }

    public IReadOnlyList<ServerMapOption> Options { get; }
    public string StartedBy { get; }
    public DateTimeOffset EndsAtUtc { get; }
    public Dictionary<ulong, string> VotesBySteamId { get; } = new();
}

public sealed class LevelCurve
{
    private long[] _xpToNextLevel = Array.Empty<long>();
    private long[] _cumulativeXp = Array.Empty<long>();

    public int MaxLevel { get; private set; } = 1;
    public long MaxTotalXp { get; private set; }

    public void Rebuild(XPXLevelsConfig config)
    {
        MaxLevel = Math.Max(1, config.MaxLevel);
        _xpToNextLevel = new long[MaxLevel + 1];
        _cumulativeXp = new long[MaxLevel + 1];

        _cumulativeXp[1] = 0;
        for (var level = 1; level < MaxLevel; level++)
        {
            var curveIndex = level - 1;
            var xpForLevel = (long)Math.Round(
                config.BaseXpToLevel +
                (config.XpLinearGrowthPerLevel * curveIndex) +
                (config.XpQuadraticGrowthPerLevel * curveIndex * curveIndex));
            _xpToNextLevel[level] = Math.Max(1L, xpForLevel);
            _cumulativeXp[level + 1] = _cumulativeXp[level] + _xpToNextLevel[level];
        }

        MaxTotalXp = _cumulativeXp[MaxLevel];
    }

    public LevelState GetState(long totalXp)
    {
        if (_cumulativeXp.Length == 0)
        {
            return new LevelState(1, 0, 0, 0);
        }

        var clampedXp = Math.Clamp(totalXp, 0, MaxTotalXp);
        var level = 1;
        for (var nextLevel = 2; nextLevel <= MaxLevel; nextLevel++)
        {
            if (clampedXp < _cumulativeXp[nextLevel])
            {
                break;
            }

            level = nextLevel;
        }

        if (level >= MaxLevel)
        {
            return new LevelState(MaxLevel, clampedXp, 0, 0);
        }

        var xpIntoLevel = clampedXp - _cumulativeXp[level];
        return new LevelState(level, clampedXp, xpIntoLevel, _xpToNextLevel[level]);
    }
}
