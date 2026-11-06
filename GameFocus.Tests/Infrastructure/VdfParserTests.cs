using System;
using Xunit;
using FluentAssertions;
using GameFocus.Infrastructure.Steam;
using System.IO;

namespace GameFocus.Tests.Infrastructure;

public class VdfParserTests
{
    [Fact]
    public void ParseAppManifest_Simple_ShouldReturnValues()
    {
        var content = "\"appid\" \"730\"\n\"installdir\" \"Counter-Strike Global Offensive\"\n\"name\" \"Counter-Strike 2\"";
        var dict = VdfParser.ParseAppManifest(content);
        dict.Should().ContainKey("appid");
        dict["appid"].Should().Be("730");
        dict["name"].Should().Be("Counter-Strike 2");
    }

    [Fact]
    public void GetLibraryPathsFromVdf_LegacyFormat_ShouldReturnPaths()
    {
        var content = "\"1\" \"D:\\SteamLibrary\\";
        var paths = VdfParser.GetLibraryPathsFromVdf(content, "C:\\Program Files\\Steam");
        paths.Should().Contain("D:\\SteamLibrary\\");
        paths.Should().Contain("C:\\Program Files\\Steam");
    }

    [Fact]
    public void ParseVdf_ModernFormat_ShouldReturnLibraryFolders()
    {
        var content = "\"libraryfolders\" { \"0\" { \"path\" \"D:\\\\SteamLibrary\" } }";
        var paths = VdfParser.GetLibraryPathsFromVdf(content, "C:\\Program Files\\Steam");
        paths.Should().Contain("D:\\SteamLibrary");
    }
}
