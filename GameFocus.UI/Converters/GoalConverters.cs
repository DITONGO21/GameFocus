using System;
using System.Globalization;
using GameFocus.Core.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GameFocus.UI.Converters;

// Human-readable name of a goal type.
public class GoalTypeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is GoalType type ? TypeName(type) : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }

    public static string TypeName(GoalType type) => type switch
    {
        GoalType.PlayMinutes => "Play time",
        GoalType.PlayConsecutiveDays => "Consecutive play days",
        GoalType.LoyaltyDays => "Loyalty (days played)",
        GoalType.CompleteCampaign => "Complete campaign",
        GoalType.RankUp => "Reach a rank",
        GoalType.Achievements => "Achievements",
        GoalType.Custom => "Custom",
        _ => type.ToString()
    };
}

// Progress text: "45 / 60 minutes" for numeric goals, "Rank: Diamond" for rank goals.
public class GoalTargetTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not Goal goal) return string.Empty;

        if (!string.IsNullOrWhiteSpace(goal.TargetLabel))
        {
            return goal.IsCompleted
                ? $"Reached: {goal.TargetLabel}"
                : $"Target rank: {goal.TargetLabel}";
        }

        var unit = goal.Type switch
        {
            GoalType.PlayMinutes => " minutes",
            GoalType.PlayConsecutiveDays => " days",
            GoalType.LoyaltyDays => " days",
            _ => string.Empty
        };
        return $"{goal.CurrentValue.ToString("0", CultureInfo.InvariantCulture)} / {goal.TargetValue.ToString("0", CultureInfo.InvariantCulture)}{unit}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

// The +1 button is only for goals whose progress is entered by hand and that are not completed yet.
public class ManualGoalVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not Goal goal) return Visibility.Collapsed;

        var manual = goal.Type is GoalType.CompleteCampaign or GoalType.Achievements or GoalType.Custom;
        return manual && !goal.IsCompleted ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
