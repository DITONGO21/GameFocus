using System;
using GameFocus.Core.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GameFocus.UI.Converters;

// Shows where a game came from, in Portuguese.
public class GameSourceTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is GameSourceType source ? SourceName(source) : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }

    public static string SourceName(GameSourceType source) => source switch
    {
        GameSourceType.Manual => "Manual (executable)",
        GameSourceType.Steam => "Steam",
        GameSourceType.Epic => "Epic Games",
        GameSourceType.Riot => "Riot Games",
        GameSourceType.Ubisoft => "Ubisoft Connect",
        GameSourceType.Xbox => "Xbox / Microsoft Store",
        GameSourceType.EA => "EA app",
        GameSourceType.BattleNet => "Battle.net",
        GameSourceType.Gog => "GOG Galaxy",
        GameSourceType.Rockstar => "Rockstar Games Launcher",
        GameSourceType.Itch => "itch.io",
        GameSourceType.Hytale => "Hytale",
        _ => source.ToString()
    };
}

// Only manual games can have their name and executable edited.
public class ManualSourceVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is GameSourceType source && source == GameSourceType.Manual
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
