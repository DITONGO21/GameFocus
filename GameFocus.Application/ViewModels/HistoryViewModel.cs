using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameFocus.Core.Entities;
using GameFocus.Core.Interfaces;

namespace GameFocus.Application.ViewModels;

public partial class HistoryViewModel : BaseViewModel
{
    private readonly IUnitOfWork _unitOfWork;

    [ObservableProperty]
    private ObservableCollection<GameSession> _sessions = new();

    [ObservableProperty]
    private bool _hasSessions;

    public HistoryViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        Title = "Session history";
    }

    [RelayCommand]
    public async Task LoadSessionsAsync()
    {
        IsBusy = true;
        try
        {
            var sessions = await _unitOfWork.Sessions.GetAllAsync();
            Sessions.Clear();
            foreach (var session in sessions.OrderByDescending(s => s.StartTimeUtc))
            {
                Sessions.Add(session);
            }
            HasSessions = Sessions.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
