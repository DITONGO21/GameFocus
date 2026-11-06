using System;
using Microsoft.UI.Dispatching;
using GameFocus.Application.Interfaces;

namespace GameFocus.UI.Services;

public class UIDispatcher : IUIDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    public UIDispatcher(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void Enqueue(Action action)
    {
        _dispatcherQueue.TryEnqueue(() => action());
    }
}
