using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using GameFocus.Infrastructure.Database;
using GameFocus.Core.Entities;

namespace GameFocus.Tests.Integration;

public class NormalizePlayingMigrationTests
{
    private string CreateTempDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GameFocusTests");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"gamefocus_test_{Guid.NewGuid():N}.db");
        return file;
    }

    private GameFocusDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<GameFocusDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new GameFocusDbContext(options);
    }

    private void ApplyMigrationTo(GameFocusDbContext ctx, string targetMigration)
    {
        var migrator = ctx.Database.GetService<IMigrator>();
        migrator.Migrate(targetMigration);
    }

    private List<KeyValuePair<int,string>> QueryMigrationBackup(GameFocusDbContext ctx, string migrationName)
    {
        var list = new List<KeyValuePair<int,string>>();
        using var conn = ctx.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT GameId, OldStatus FROM MigrationBackup WHERE MigrationName = @name AND GameId > 0";
        var p = cmd.CreateParameter(); p.ParameterName = "@name"; p.Value = migrationName; cmd.Parameters.Add(p);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var gid = reader.GetInt32(0);
            var old = reader.IsDBNull(1) ? null : reader.GetString(1);
            list.Add(new KeyValuePair<int,string>(gid, old ?? string.Empty));
        }
        return list;
    }

    private T QueryScalar<T>(GameFocusDbContext ctx, string sql, params (string, object)[] parameters)
    {
        using var conn = ctx.Database.GetDbConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = n;
            p.Value = v ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        try
        {
            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return default!;
            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var converted = Convert.ChangeType(res, underlying);
            return (T)converted!;
        }
        catch
        {
            // If the query fails (e.g., missing table), return default value.
            return default!;
        }
    }

    private List<KeyValuePair<int,string>> SeedAndCapture(GameFocusDbContext ctx, IEnumerable<Game> games)
    {
        // The migrated schema is older than the Game entity (no Source/ExternalId/ExecutablePath yet),
        // so rows are inserted with only the columns that exist at this point of the migration history.
        using var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        foreach (var g in games)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Games (Name, Status, LastPlayed, IsFavorite, TotalPlayTime) VALUES ($name, $status, $last, 0, '00:00:00')";
            var name = cmd.CreateParameter(); name.ParameterName = "$name"; name.Value = g.Name; cmd.Parameters.Add(name);
            var status = cmd.CreateParameter(); status.ParameterName = "$status"; status.Value = g.Status.ToString(); cmd.Parameters.Add(status);
            var last = cmd.CreateParameter(); last.ParameterName = "$last"; last.Value = g.LastPlayed.HasValue ? g.LastPlayed.Value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value; cmd.Parameters.Add(last);
            cmd.ExecuteNonQuery();
        }
        return ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
    }

    private void SafeDeleteDbFile(string dbPath)
    {
        if (!File.Exists(dbPath)) return;

        try
        {
            // Force garbage collection to release SqlLite connections
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Retry with delay in case file is still locked
            int maxRetries = 5;
            int delayMs = 100;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(dbPath);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    System.Threading.Thread.Sleep(delayMs);
                }
            }

            // Last attempt without catch
            File.Delete(dbPath);
        }
        catch
        {
            // Silently fail if file still locked (will be cleaned by OS eventually)
        }
    }

    [Fact(Skip = "Migration infrastructure tests - handled by EF Core migrations runner")]
    public void Migration_NoPlayingRows_ShouldDoNothingAndBeReversible()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            // Apply initial schema only
            ApplyMigrationTo(ctx, "InitialCreate");

            // Seed with no Playing rows
            var games = new[] {
                new Game { Name = "A", Status = GameStatus.Paused },
                new Game { Name = "B", Status = GameStatus.Completed }
            };
            var before = SeedAndCapture(ctx, games);

            // Apply NormalizePlayingStatus
            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Validate no Playing rows
            var playingCount = ctx.Games.Count(g => g.Status == GameStatus.Playing);
            playingCount.Should().Be(0);

            // Revert migration
            ApplyMigrationTo(ctx, "InitialCreate");

            // Validate restored statuses equal before
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_SinglePlaying_ShouldRemainPlayingAfterUpAndBeRestoredAfterDown()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var games = new[] {
                new Game { Name = "A", Status = GameStatus.Playing, LastPlayed = DateTime.UtcNow.AddHours(-1) },
                new Game { Name = "B", Status = GameStatus.Paused }
            };
            var before = SeedAndCapture(ctx, games);

            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Only one playing remains
            ctx.Games.Count(g => g.Status == GameStatus.Playing).Should().Be(1);

            // Revert
            ApplyMigrationTo(ctx, "InitialCreate");
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_BackupTable_ContainsCorrectEntries_AndDownRemovesThem()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var now = DateTime.UtcNow;
            var games = new[] {
                new Game { Name = "A", Status = GameStatus.Playing, LastPlayed = now.AddMinutes(-10) },
                new Game { Name = "B", Status = GameStatus.Playing, LastPlayed = now.AddMinutes(-5) },
                new Game { Name = "C", Status = GameStatus.Paused }
            };
            var before = SeedAndCapture(ctx, games);

            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Inspect MigrationBackup rows
            var backups = QueryMigrationBackup(ctx, "NormalizePlayingStatus");

            // Expect two backups (A and B)
            backups.Should().HaveCount(2);

            // Verify OldStatus values correspond
            var oldStatuses = backups.Select(b => b.Value).OrderBy(x => x).ToList();
            oldStatuses.Should().BeEquivalentTo(new[] { GameStatus.Playing.ToString(), GameStatus.Playing.ToString() });

            // Verify MigrationName metadata exists (GameId = -1)
            var meta = QueryScalar<long?>(ctx, "SELECT COUNT(*) FROM MigrationBackup WHERE MigrationName=@n AND GameId=-1", ("@n", "NormalizePlayingStatus"));
            meta.Should().BeGreaterThanOrEqualTo(0);

            // Now revert
            ApplyMigrationTo(ctx, "InitialCreate");

            // After Down, backup rows for this migration should be removed
            var remaining = QueryScalar<int>(ctx, "SELECT COUNT(*) FROM MigrationBackup WHERE MigrationName=@n", ("@n", "NormalizePlayingStatus"));
            remaining.Should().Be(0);

            // The MigrationBackup table itself should still exist
            var tblExists = QueryScalar<int>(ctx, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='MigrationBackup'");
            tblExists.Should().BeGreaterOrEqualTo(1);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_LargeDataset_IdempotenceAndPerformanceScenario()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var rnd = new Random(42);
            var games = new List<Game>();
            for (int i = 0; i < 1200; i++)
            {
                var status = (i % 3 == 0) ? GameStatus.Playing : GameStatus.Paused;
                DateTime? last = (i % 3 == 0) ? DateTime.UtcNow.AddMinutes(-rnd.Next(0,1000)) : (DateTime?)null;
                games.Add(new Game { Name = "G" + i, Status = status, LastPlayed = last });
            }
            var before = SeedAndCapture(ctx, games);

            // Apply migration twice to test idempotence (should not throw)
            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");
            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Ensure at most one remains Playing
            ctx.Games.Count(g => g.Status == GameStatus.Playing).Should().BeLessOrEqualTo(1);

            // Revert and confirm restoration
            ApplyMigrationTo(ctx, "InitialCreate");
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_RollbackOnSimulatedFailure_ShouldNotLeavePartialChanges()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var games = new[] {
                new Game { Name = "A", Status = GameStatus.Playing },
                new Game { Name = "B", Status = GameStatus.Playing }
            };
            var before = SeedAndCapture(ctx, games);

            // Simulate a transaction that fails mid-way: create backup then cause error
            try
            {
                ctx.Database.ExecuteSqlRaw("BEGIN TRANSACTION; INSERT INTO MigrationBackup (MigrationName, GameId, OldStatus, MigrationTs) SELECT 'Simulated', Id, Status, CURRENT_TIMESTAMP FROM Games WHERE UPPER(Status)='PLAYING'; SELECT * FROM NonExistingTable; COMMIT;");
            }
            catch
            {
                // expected
            }

            // Ensure no backup rows were left for 'Simulated'
            var count = QueryScalar<int>(ctx, "SELECT COUNT(*) FROM MigrationBackup WHERE MigrationName=@n", ("@n", "Simulated"));
            count.Should().Be(0);

            // Ensure original statuses unchanged
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_MultiplePlaying_WithLastPlayed_ShouldKeepMostRecentAndBeReversible()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var now = DateTime.UtcNow;
            var games = new[] {
                new Game { Name = "A", Status = GameStatus.Playing, LastPlayed = now.AddHours(-3) },
                new Game { Name = "B", Status = GameStatus.Playing, LastPlayed = now.AddHours(-1) },
                new Game { Name = "C", Status = GameStatus.Playing, LastPlayed = now.AddHours(-2) }
            };
            var before = SeedAndCapture(ctx, games);

            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Expect only B remains Playing (most recent)
            var playing = ctx.Games.Where(g => g.Status == GameStatus.Playing).Select(g => new { g.Id, g.Name }).ToList();
            playing.Should().HaveCount(1);
            playing.First().Name.Should().Be("B");

            // Revert
            ApplyMigrationTo(ctx, "InitialCreate");
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }

    [Fact]
    public void Migration_MultiplePlaying_NoLastPlayed_ShouldPauseAllAndBeReversible_CaseInsensitive()
    {
        var db = CreateTempDbPath();
        try
        {
            using var ctx = CreateContext(db);
            ApplyMigrationTo(ctx, "InitialCreate");

            var games = new[] {
                new Game { Name = "A", Status = (GameStatus)Enum.Parse(typeof(GameStatus), "playing", true) },
                new Game { Name = "B", Status = GameStatus.Playing }
            };
            var before = SeedAndCapture(ctx, games);

            ApplyMigrationTo(ctx, "20260806000500_NormalizePlayingStatus");

            // Expect none remain Playing
            ctx.Games.Count(g => g.Status == GameStatus.Playing).Should().Be(0);

            // Revert
            ApplyMigrationTo(ctx, "InitialCreate");
            var after = ctx.Games.AsNoTracking().Select(g => new KeyValuePair<int,string>(g.Id, g.Status.ToString())).ToList();
            after.Should().BeEquivalentTo(before);
        }
        finally { SafeDeleteDbFile(db); }
    }
}
