using System;
using GameFocus.Application.Interfaces;

namespace GameFocus.Application.Services;

public class DashboardPublisher : IDashboardPublisher
{
    public event EventHandler<object>? OnSessionUpdated;

    public void PublishSessionUpdated(object payload)
    {
        Console.WriteLine($"DashboardPublisher: publishing payload={payload}");
        OnSessionUpdated?.Invoke(this, payload);
    }
}
