using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using GameFocus.Core.Entities;

namespace GameFocus.UI.Views;

public sealed partial class IntentWindow : Window
{
    private readonly int _sessionId;
    private readonly Action<int, PlayIntention> _onSelected;
    private readonly DispatcherTimer _timer;

    public IntentWindow(int sessionId, Action<int, PlayIntention> onSelected)
    {
        this.InitializeComponent();
        _sessionId = sessionId;
        _onSelected = onSelected;

        // Try to set compact overlay (always on top)
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        
        if (appWindow.Presenter is OverlappedPresenter op)
        {
            op.SetBorderAndTitleBar(false, false);
            op.IsAlwaysOnTop = true;
            op.IsResizable = false;
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 300, Height = 180 });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (s, e) => {
            _timer.Stop();
            this.Close();
        };
        _timer.Start();
    }

    private void BtnRelaxar_Click(object sender, RoutedEventArgs e) => Submit(PlayIntention.Relaxar);
    private void BtnCompetir_Click(object sender, RoutedEventArgs e) => Submit(PlayIntention.Competir);
    private void BtnTreinar_Click(object sender, RoutedEventArgs e) => Submit(PlayIntention.Treinar);
    private void BtnPassarTempo_Click(object sender, RoutedEventArgs e) => Submit(PlayIntention.PassarTempo);

    private void Submit(PlayIntention intent)
    {
        _timer.Stop();
        _onSelected?.Invoke(_sessionId, intent);
        this.Close();
    }
}
