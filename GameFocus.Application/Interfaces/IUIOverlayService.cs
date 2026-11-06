using System;
using GameFocus.Core.Entities;

namespace GameFocus.Application.Interfaces;

public interface IUIOverlayService
{
    void ShowIntentOverlay(int sessionId, Action<int, PlayIntention> onIntentSelected);
    void ShowFeedbackOverlay(int sessionId, Action<int, SessionEnjoyment> onEnjoymentSelected);
}
