using Avalonia.Controls;

namespace Gallean.Client;

internal sealed partial class FatalErrorWindow : Window
{
    public FatalErrorWindow()
    {
        InitializeComponent();
    }

    public FatalErrorWindow(string title, string message, string? logLocation)
        : this()
    {
        Content = new FatalErrorView(title, message, logLocation);
    }

    private FatalErrorView View => (FatalErrorView)Content!;

    internal TextBlock TitleText => View.TitleText;
    internal TextBlock MessageText => View.MessageText;
    internal TextBlock LogLocationText => View.LogLocationText;
}
