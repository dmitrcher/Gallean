using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Gallean.Client;

internal sealed partial class AssetPackErrorWindow : Window
{
    public AssetPackErrorWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
