using System;

namespace GameFocus.Application.Interfaces;

public interface IDashboardPublisher
{
    event EventHandler<object>? OnSessionUpdated;
    void PublishSessionUpdated(object payload);
}
