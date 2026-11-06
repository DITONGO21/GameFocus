using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameFocus.Infrastructure.Migrations
{

    public partial class NormalizePlayingStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safe reversible migration: create a generic MigrationBackup table,
            // back up current statuses for this migration name, then attempt to keep
            // the most recently played game (LastPlayed) as Playing and pause the rest.
            // Uses case-insensitive comparison for status.

            Serilog.Log.Information("NormalizePlayingStatus Up starting");

var sql = @"
-- Generic backup table for migrations (reusable)
CREATE TABLE IF NOT EXISTS MigrationBackup (
    MigrationName TEXT NOT NULL,
    GameId INTEGER NOT NULL,
    OldStatus TEXT,
    MigrationTs TEXT,
    PRIMARY KEY (MigrationName, GameId)
);

-- Backup current Playing statuses (case-insensitive)
INSERT OR REPLACE INTO MigrationBackup (MigrationName, GameId, OldStatus, MigrationTs)
SELECT 'NormalizePlayingStatus', Id, Status, CURRENT_TIMESTAMP FROM Games WHERE UPPER(Status) = 'PLAYING';

-- Store a metadata row with count as GameId = -1
INSERT OR REPLACE INTO MigrationBackup (MigrationName, GameId, OldStatus, MigrationTs)
VALUES ('NormalizePlayingStatus', -1, (SELECT COUNT(*) FROM MigrationBackup WHERE MigrationName = 'NormalizePlayingStatus'), CURRENT_TIMESTAMP);

-- First attempt: pause all Playing except the most recently LastPlayed (tie-breaker Id DESC)
UPDATE Games
SET Status = 'Paused'
WHERE UPPER(Status) = 'PLAYING'
  AND Id != (
    SELECT Id FROM Games WHERE UPPER(Status) = 'PLAYING' AND LastPlayed IS NOT NULL ORDER BY LastPlayed DESC, Id DESC LIMIT 1
  );

-- Second attempt: if no LastPlayed exists among Playing entries, pause all Playing entries
UPDATE Games
SET Status = 'Paused'
WHERE UPPER(Status) = 'PLAYING' AND NOT EXISTS (SELECT 1 FROM Games WHERE UPPER(Status) = 'PLAYING' AND LastPlayed IS NOT NULL);
";

            migrationBuilder.Sql(sql);

            Serilog.Log.Information("NormalizePlayingStatus Up completed");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore statuses from the generic MigrationBackup table for this migration
            Serilog.Log.Information("NormalizePlayingStatus Down starting");

            var down = @"
-- Restore statuses from backup for this migration
UPDATE Games
SET Status = (
    SELECT OldStatus FROM MigrationBackup WHERE MigrationName = 'NormalizePlayingStatus' AND GameId = Games.Id
)
WHERE Id IN (SELECT GameId FROM MigrationBackup WHERE MigrationName = 'NormalizePlayingStatus' AND GameId > 0);

-- Remove backup rows for this migration (keep table for reuse)
DELETE FROM MigrationBackup WHERE MigrationName = 'NormalizePlayingStatus';
";

            migrationBuilder.Sql(down);

            Serilog.Log.Information("NormalizePlayingStatus Down completed");
        }
    }
}

