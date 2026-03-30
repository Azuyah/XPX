using Dapper;
using CounterStrikeSharp.API.Core;
using Microsoft.Data.Sqlite;

namespace XPXLevels;

public sealed class XPXLevelsRepository
{
    private readonly string _databasePath;

    public XPXLevelsRepository(string moduleDirectory)
    {
        var dataDirectory = Path.Combine(Application.RootDirectory, "data", "XPXLevels");
        Directory.CreateDirectory(dataDirectory);
        _databasePath = Path.Combine(dataDirectory, "xpx-levels.db");
        MigrateLegacyDatabase(moduleDirectory);
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS players (
                steamid INTEGER PRIMARY KEY,
                player_name TEXT NOT NULL,
                total_xp INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );
            """);

        connection.Execute("""
            CREATE INDEX IF NOT EXISTS idx_players_total_xp
            ON players (total_xp DESC);
            """);
    }

    public PlayerProgress GetOrCreatePlayer(ulong steamId, string playerName)
    {
        using var connection = OpenConnection();
        var row = GetPlayerRow(connection, steamId);

        if (row is null)
        {
            var created = new PlayerProgress
            {
                SteamId = steamId,
                PlayerName = playerName,
                TotalXp = 0
            };

            SavePlayer(created);
            return created;
        }

        if (!string.Equals(row.PlayerName, playerName, StringComparison.Ordinal))
        {
            connection.Execute(
                """
                UPDATE players
                SET player_name = @PlayerName,
                    updated_utc = @UpdatedUtc
                WHERE steamid = @SteamId;
                """,
                new
                {
                    SteamId = (long)steamId,
                    PlayerName = playerName,
                    UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
                });
        }

        return new PlayerProgress
        {
            SteamId = steamId,
            PlayerName = playerName,
            TotalXp = row.TotalXp
        };
    }

    public PlayerProgress? GetPlayer(ulong steamId)
    {
        using var connection = OpenConnection();
        var row = GetPlayerRow(connection, steamId);
        if (row is null)
        {
            return null;
        }

        return new PlayerProgress
        {
            SteamId = steamId,
            PlayerName = row.PlayerName,
            TotalXp = row.TotalXp
        };
    }

    public void SavePlayer(PlayerProgress progress)
    {
        using var connection = OpenConnection();
        connection.Execute(
            """
            INSERT INTO players (steamid, player_name, total_xp, created_utc, updated_utc)
            VALUES (@SteamId, @PlayerName, @TotalXp, @NowUtc, @NowUtc)
            ON CONFLICT(steamid) DO UPDATE SET
                player_name = excluded.player_name,
                total_xp = excluded.total_xp,
                updated_utc = excluded.updated_utc;
            """,
            new
            {
                SteamId = (long)progress.SteamId,
                progress.PlayerName,
                progress.TotalXp,
                NowUtc = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    public (int Rank, int TotalPlayers) GetRank(ulong steamId, long totalXp)
    {
        using var connection = OpenConnection();
        var totalPlayers = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM players;");
        if (totalPlayers <= 0)
        {
            return (0, 0);
        }

        var rank = connection.ExecuteScalar<int>(
            """
            SELECT COUNT(*) + 1
            FROM players
            WHERE total_xp > @TotalXp
               OR (total_xp = @TotalXp AND steamid < @SteamId);
            """,
            new
            {
                SteamId = (long)steamId,
                TotalXp = totalXp
            });

        return (rank, totalPlayers);
    }

    public IReadOnlyList<TopPlayerEntry> GetTopPlayers(int limit)
    {
        using var connection = OpenConnection();
        var rows = connection.Query<PlayerRow>(
            """
            SELECT steamid, player_name, total_xp
            FROM players
            ORDER BY total_xp DESC, steamid ASC
            LIMIT @Limit;
            """,
            new { Limit = Math.Max(1, limit) }).ToList();

        return rows.Select((row, index) => new TopPlayerEntry(index + 1, (ulong)row.SteamId, row.PlayerName, row.TotalXp)).ToList();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static PlayerRow? GetPlayerRow(SqliteConnection connection, ulong steamId)
    {
        return connection.QuerySingleOrDefault<PlayerRow>(
            """
            SELECT steamid, player_name, total_xp
            FROM players
            WHERE steamid = @SteamId;
            """,
            new { SteamId = (long)steamId });
    }

    private void MigrateLegacyDatabase(string moduleDirectory)
    {
        if (File.Exists(_databasePath))
        {
            return;
        }

        var pluginRoot = Directory.GetParent(moduleDirectory)?.FullName;
        var candidates = new List<string>
        {
            Path.Combine(moduleDirectory, "xpx-levels.db"),
            Path.Combine(moduleDirectory, "XPX-levels.db")
        };

        if (!string.IsNullOrWhiteSpace(pluginRoot) && Directory.Exists(pluginRoot))
        {
            foreach (var siblingDirectory in Directory.GetDirectories(pluginRoot)
                         .Where(path => !string.Equals(path, moduleDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.AddRange(Directory.EnumerateFiles(siblingDirectory, "*levels.db", SearchOption.TopDirectoryOnly));
            }
        }

        foreach (var candidate in candidates.Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
        {
            File.Copy(candidate!, _databasePath, overwrite: false);
            break;
        }
    }

    private sealed class PlayerRow
    {
        public long SteamId { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public long TotalXp { get; init; }
    }
}
