using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace Gallean.Client;

internal sealed partial class AssetPackErrorView : UserControl
{
    private const string AssetPackExtension = ".galleanpack";

    private bool importing;

    public AssetPackErrorView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private AssetPackErrorViewModel ViewModel => (AssetPackErrorViewModel)DataContext!;

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ClientPlatform.ImportsAssetPack)
        {
            ImportAssetPackAsync(sender as Button).FileAndForget();
        }
        else
        {
            DirectoryInfo directory = new(ViewModel.Location);

            TopLevel.GetTopLevel(this)?.Launcher.LaunchDirectoryInfoAsync(directory).FileAndForget();
        }
    }

    /// <summary>
    /// Copies a pack that the player picks into the asset folder, for platforms where the folder is hard to reach.
    /// </summary>
    private async Task ImportAssetPackAsync(Button? button)
    {
        if (importing || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false
        });

        if (files.Count == 0)
        {
            return;
        }

        importing = true;

        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            IStorageFile file = files[0];
            string name = Path.GetFileName(file.Name);

            if (!name.EndsWith(AssetPackExtension, StringComparison.OrdinalIgnoreCase))
            {
                name = Path.ChangeExtension(name, AssetPackExtension);
            }

            Directory.CreateDirectory(ViewModel.Location);

            string target = Path.Combine(ViewModel.Location, name);
            string partial = target + ".part";

            await using (Stream source = await file.OpenReadAsync())
            await using (FileStream destination = new(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            {
                await source.CopyToAsync(destination);
            }

            File.Move(partial, target, overwrite: true);

            ViewModel.RestartCommand.Execute(null);
        }
        finally
        {
            importing = false;

            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }
}
