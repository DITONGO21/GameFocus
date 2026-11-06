using System;
using GameFocus.Application.Interfaces;
using GameFocus.Core.Entities;
using GameFocus.UI.Views;
using Microsoft.UI.Dispatching;

namespace GameFocus.UI.Services;

public class UIOverlayService : IUIOverlayService
{
    private readonly DispatcherQueue _dispatcher;

    private readonly Microsoft.Extensions.Logging.ILogger<UIOverlayService>? _logger;

    public UIOverlayService(DispatcherQueue dispatcher, Microsoft.Extensions.Logging.ILogger<UIOverlayService>? logger = null)
    {
        _dispatcher = dispatcher ?? DispatcherQueue.GetForCurrentThread();
        _logger = logger;
    }

    public void ShowIntentOverlay(int sessionId, Action<int, PlayIntention> onIntentSelected)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var win = new IntentWindow(sessionId, onIntentSelected);
            win.Activate();
        });
    }

    public void ShowFeedbackOverlay(int sessionId, Action<int, SessionEnjoyment> onEnjoymentSelected)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var win = new FeedbackWindow(sessionId, onEnjoymentSelected);
            win.Activate();
        });
    }
}
