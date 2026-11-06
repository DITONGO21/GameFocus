using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using GameFocus.Core.Entities;

namespace GameFocus.UI.Views;

public sealed partial class FeedbackWindow : Window
{
    private readonly int _sessionId;
    private readonly Action<int, SessionEnjoyment> _onSelected;
    private readonly DispatcherTimer _timer;

    public FeedbackWindow(int sessionId, Action<int, SessionEnjoyment> onSelected)
    {
        this.InitializeComponent();
        _sessionId = sessionId;
        _onSelected = onSelected;

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        
        if (appWindow.Presenter is OverlappedPresenter op)
        {
            op.SetBorderAndTitleBar(false, false);
            op.IsAlwaysOnTop = true;
            op.IsResizable = false;
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 300, Height = 130 });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (s, e) => {
            _timer.Stop();
            this.Close();
        };
        _timer.Start();
    }

    private void BtnNaoGostei_Click(object sender, RoutedEventArgs e) => Submit(SessionEnjoyment.NaoGostei);
    private void BtnNeutro_Click(object sender, RoutedEventArgs e) => Submit(SessionEnjoyment.Neutro);
    private void BtnGostei_Click(object sender, RoutedEventArgs e) => Submit(SessionEnjoyment.Gostei);
    private void BtnAdorei_Click(object sender, RoutedEventArgs e) => Submit(SessionEnjoyment.Adorei);

    private void Submit(SessionEnjoyment enjoyment)
    {
        _timer.Stop();
        _onSelected?.Invoke(_sessionId, enjoyment);
        this.Close();
    }
}
