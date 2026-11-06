using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using GameFocus.Infrastructure.Database;
using GameFocus.Core.Entities;

namespace E2ETester
{
    class Program
    {
        static void Main(string[] args)
        {
            var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameFocus");
            Directory.CreateDirectory(dbDir);
            var dbPath = Path.Combine(dbDir, "gamefocus.db");

            var options = new DbContextOptionsBuilder<GameFocusDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using (var db = new GameFocusDbContext(options))
            {
                db.Database.EnsureCreated();

                if (args.Length > 0 && args[0] == "seed")
                {
                    if (!db.Games.Any(g => g.Name == "notepad"))
                    {
                        db.Games.Add(new Game { Name = "notepad", Status = GameStatus.Playing });
                        db.SaveChanges();
                        Console.WriteLine("Seeded notepad.");
                    }
                }
                else if (args.Length > 0 && args[0] == "verify")
                {
                    var game = db.Games.Include(g => g.Sessions).FirstOrDefault(g => g.Name == "notepad");
                    if (game == null)
                    {
                        Console.WriteLine("ERROR: Notepad game not found in DB.");
                        return;
                    }

                    var session = game.Sessions.OrderByDescending(s => s.StartTimeUtc).FirstOrDefault();
                    if (session == null)
                    {
                        Console.WriteLine("ERROR: No session found for Notepad.");
                        return;
                    }

                    if (session.EndTimeUtc == null)
                    {
                        Console.WriteLine("ERROR: Session EndTime is null.");
                        return;
                    }

                    Console.WriteLine("VERIFICATION SUCCESSFUL:");
                    Console.WriteLine($"- Game: {game.Name}");
                    Console.WriteLine($"- Session Start: {session.StartTimeUtc}");
                    Console.WriteLine($"- Session End: {session.EndTimeUtc}");
                    Console.WriteLine($"- Session Duration: {session.DurationSeconds} secs");
                    Console.WriteLine($"- Game Last Played: {game.LastPlayed}");
                    Console.WriteLine($"- Game Total PlayTime: {game.TotalPlayTime.TotalMinutes} mins");
                }
            }
        }
    }
}
