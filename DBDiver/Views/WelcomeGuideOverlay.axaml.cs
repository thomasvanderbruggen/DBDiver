using Avalonia.Controls;

namespace DBDiver.Views;

public partial class WelcomeGuideOverlay : UserControl
{
    public event Action? CloseRequested;

    public WelcomeGuideOverlay()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => CloseRequested?.Invoke();
    }
}
