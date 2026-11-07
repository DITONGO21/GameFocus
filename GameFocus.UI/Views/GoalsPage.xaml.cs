using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using GameFocus.Application.ViewModels;
using GameFocus.Core.Entities;

namespace GameFocus.UI.Views;

public sealed partial class GoalsPage : Page
{
    public GoalsViewModel ViewModel { get; }

    public GoalsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<GoalsViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadDataAsync();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = ViewModel.HasGoals ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void AddGoal_Click(object sender, RoutedEventArgs e)
    {
        GoalTitleBox.Text = string.Empty;
        GameCombo.SelectedIndex = -1;
        GoalTypeCombo.SelectedIndex = 0;
        TargetValueBox.Value = 60;
        TargetLabelBox.Text = string.Empty;

        if (!ViewModel.HasGames)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = "No games",
                Content = "Add a game to the Library first, before creating a goal.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await noGamesDialog.ShowAsync();
            return;
        }

        await AddGoalDialog.ShowAsync();
    }

    private async void AddGoalDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.NewGoalTitle = GoalTitleBox.Text;
        ViewModel.SelectedGameForGoal = GameCombo.SelectedItem as Game;
        ViewModel.SelectedGoalTypeIndex = GoalTypeCombo.SelectedIndex;
        ViewModel.NewGoalTargetValue = TargetValueBox.Value;
        ViewModel.NewGoalTargetLabel = TargetLabelBox.Text;

        await ViewModel.AddGoalCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    // Rank goals take a rank name instead of a number.
    private void GoalTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetValueBox == null || TargetLabelBox == null) return;

        var isRank = GoalTypeCombo.SelectedIndex == (int)GameFocus.Core.Entities.GoalType.RankUp;
        TargetValueBox.Visibility = isRank ? Visibility.Collapsed : Visibility.Visible;
        TargetLabelBox.Visibility = isRank ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Increment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Goal goal)
        {
            await ViewModel.IncrementGoalCommand.ExecuteAsync(goal);
        }
    }

    private async void Complete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Goal goal)
        {
            await ViewModel.CompleteGoalCommand.ExecuteAsync(goal);
            UpdateEmptyState();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Goal goal)
        {
            await ViewModel.DeleteGoalCommand.ExecuteAsync(goal);
            UpdateEmptyState();
        }
    }
}
