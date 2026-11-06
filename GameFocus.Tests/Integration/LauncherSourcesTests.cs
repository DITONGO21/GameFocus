using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GameFocus.Core.Entities;
using GameFocus.Infrastructure.GameSources;
using Xunit;

namespace GameFocus.Tests.Integration;

// Launcher sources, tested with temporary folders and a fake registry. Nothing reads real launcher data.
public class LauncherSourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gf_sources_{Guid.NewGuid():N}");

    public LauncherSourcesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FakeRegistry : IWindowsRegistry
    {
        private readonly Dictionary<string, Dictionary<string, string>> _keys = new(StringComparer.OrdinalIgnoreCase);

        public FakeRegistry Add(string keyPath, string valueName, string value)
        {
            if (!_keys.TryGetValue(keyPath, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _keys[keyPath] = values;
            }
            values[valueName] = value;
            return this;
        }

        public IEnumerable<string> GetSubKeyNames(string keyPath)
        {
            var prefix = keyPath + @"\";
            return _keys.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Substring(prefix.Length).Split('\\')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string? GetString(string keyPath, string valueName) =>
            _keys.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out var v) ? v : null;
    }

    [Fact]
    public async Task Epic_ReadsManifests_AndSkipsIncompleteInstalls()
    {
        var folder = Path.Combine(_root, "Manifests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.item"),
            "{\"AppName\":\"Rocket\",\"DisplayName\":\"Rocket League\",\"InstallLocation\":\"C:/Epic/RL\",\"LaunchExecutable\":\"RL.exe\"}");
        File.WriteAllText(Path.Combine(folder, "b.item"),
            "{\"AppName\":\"Broken\",\"DisplayName\":\"Broken\",\"bIsIncompleteInstall\":true}");

        var games = await new EpicGameSource(folder).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Source.Should().Be(GameSourceType.Epic);
        games[0].ExternalId.Should().Be("Rocket");
        games[0].Name.Should().Be("Rocket League");
    }

    [Fact]
    public async Task Gog_ReadsRegistryEntries_WithoutInventingExecutablePaths()
    {
        const string key = @"SOFTWARE\WOW6432Node\GOG.com\Games\1207658924";
        var registry = new FakeRegistry()
            .Add(key, "gameName", "Witcher 3")
            .Add(key, "path", @"C:\GOG Games\Witcher 3");

        var games = await new GogGameSource(registry).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Witcher 3");
        games[0].ExternalId.Should().Be("1207658924");
        games[0].ExecutablePath.Should().BeNull();
    }

    [Fact]
    public async Task Ubisoft_UsesInstallFolderName()
    {
        const string key = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\820";
        var registry = new FakeRegistry().Add(key, "InstallDir", @"D:\Games\Assassins Creed Odyssey\");

        var games = await new UbisoftGameSource(registry).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Assassins Creed Odyssey");
        games[0].Source.Should().Be(GameSourceType.Ubisoft);
    }

    [Fact]
    public async Task Rockstar_IgnoresLauncherKey()
    {
        var registry = new FakeRegistry()
            .Add(@"SOFTWARE\WOW6432Node\Rockstar Games\Launcher", "InstallFolder", @"C:\Rockstar\Launcher")
            .Add(@"SOFTWARE\WOW6432Node\Rockstar Games\Social Club", "InstallFolder", @"C:\Rockstar\Social Club")
            .Add(@"SOFTWARE\WOW6432Node\Rockstar Games\GTAV", "InstallFolder", @"C:\Games\GTAV");

        var games = await new RockstarGameSource(registry).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].ExternalId.Should().Be("GTAV");
    }

    [Fact]
    public async Task Ea_ReadsLegacyInstallDir()
    {
        var registry = new FakeRegistry()
            .Add(@"SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Battlefield 4", "Install Dir", @"C:\EA\BF4");

        var games = await new EaGameSource(registry).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Battlefield 4");
    }

    [Fact]
    public async Task Riot_MapsValorantLiveFolderToGameName()
    {
        var league = Path.Combine(_root, "Riot Games", "League of Legends");
        var valorant = Path.Combine(_root, "Riot Games", "VALORANT", "live");
        Directory.CreateDirectory(league);
        Directory.CreateDirectory(valorant);
        var json = "{\"associated_client\":{\"" + league.Replace('\\', '/') + "/\":\"x\",\"" +
                   valorant.Replace('\\', '/') + "/\":\"x\"}}";

        var games = RiotGameSource.Parse(json);

        games.Select(g => g.Name).Should().BeEquivalentTo(new[] { "League of Legends", "VALORANT" });
    }

    [Fact]
    public async Task Xbox_ListsOneGamePerTitleFolder()
    {
        var folder = Path.Combine(_root, "XboxGames");
        var game = Path.Combine(folder, "Forza Horizon 5", "Content");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "ForzaHorizon5.exe"), new byte[] { 0x4D, 0x5A });
        Directory.CreateDirectory(Path.Combine(folder, "GameSave"));
        Directory.CreateDirectory(Path.Combine(folder, "Minecraft Launcher"));

        var games = await new XboxGameSource(folder).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Forza Horizon 5");
        games[0].Source.Should().Be(GameSourceType.Xbox);
    }

    [Fact]
    public async Task Xbox_IncludesMinecraftFolderThatHoldsTheGame()
    {
        var folder = Path.Combine(_root, "XboxGames");
        var content = Path.Combine(folder, "Minecraft Launcher", "Content");
        Directory.CreateDirectory(content);
        File.WriteAllBytes(Path.Combine(content, "Minecraft.exe"), new byte[] { 0x4D, 0x5A });

        var games = await new XboxGameSource(folder).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Minecraft Launcher");
    }

    [Fact]
    public async Task Hytale_FindsClientExecutableInInstallChannel()
    {
        var client = Path.Combine(_root, "Hytale", "install", "release", "package", "game", "latest", "Client");
        Directory.CreateDirectory(client);
        File.WriteAllBytes(Path.Combine(client, "HytaleClient.exe"), new byte[] { 0x4D, 0x5A });

        var games = await new HytaleGameSource(Path.Combine(_root, "Hytale", "install")).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Hytale");
        games[0].Source.Should().Be(GameSourceType.Hytale);
        games[0].ExecutablePath.Should().Be(Path.Combine(client, "HytaleClient.exe"));
    }

    [Fact]
    public async Task Ubisoft_IgnoresUserRecordWithoutInstallFolder()
    {
        var registry = new FakeRegistry()
            .Add(@"HKCU\SOFTWARE\Ubisoft\Launcher\Installs\5095", "InstallState", "1");

        var games = await new UbisoftGameSource(registry).DiscoverAsync();

        games.Should().BeEmpty();
    }

    [Fact]
    public async Task Ubisoft_ReadsUserRecordWithInstallFolder()
    {
        var registry = new FakeRegistry()
            .Add(@"HKCU\SOFTWARE\Ubisoft\Launcher\Installs\42", "InstallDir", @"D:\Games\Far Cry 6\");

        var games = await new UbisoftGameSource(registry).DiscoverAsync();

        games.Should().ContainSingle();
        games[0].Name.Should().Be("Far Cry 6");
    }

    [Fact]
    public async Task MissingLauncherData_ReturnsNothing()
    {
        var missing = Path.Combine(_root, "does-not-exist");

        (await new EpicGameSource(missing).DiscoverAsync()).Should().BeEmpty();
        (await new XboxGameSource(missing).DiscoverAsync()).Should().BeEmpty();
        (await new RiotGameSource(Path.Combine(missing, "x.json")).DiscoverAsync()).Should().BeEmpty();
    }
}
