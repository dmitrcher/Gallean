using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Gallean.Common;
using Gallean.Configuration;

namespace Gallean.Client;

internal sealed class App(ILog log, GCMemoryInfo memoryInfo) : Application
{
    private CleanupList cleanupList = new();
    private GameViewModel? currentGame;
    private Exception? currentGameException;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ThemeAccentResources.Register(Resources, Theme.Accent.Default);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Mobile platforms deliver input from their own main loop, outside Avalonia's dispatcher jobs, so without
        // this the continuations of awaits started by input run on the thread pool and build views off the UI thread.
        AvaloniaSynchronizationContext.InstallIfNeeded();

        IGameHost? host = ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => new DesktopGameHost(desktop),
            ISingleViewApplicationLifetime singleView => new SingleViewGameHost(singleView),
            _ => null
        };

        if (host is not null)
        {
            Dispatcher.UIThread.UnhandledException += (_, ev) => OnUnhandledException(host, ev);
            host.Exiting += (_, _) => cleanupList.Dispose();
            _ = TryStartGameAsync(host);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task TryStartGameAsync(IGameHost host)
    {
        try
        {
            await StartGameAsync(host);
        }
        catch (Exception ex)
        {
            log.Error("Failed to start the game.", ex);
            cleanupList.Dispose();
            HandleStartupException(host, ex);
        }
    }

    private async Task StartGameAsync(IGameHost host)
    {
        GameSettingsStore gameSettingsStore = new(AppDirectories.Root);
        GameSettings gameSettings = await LoadSettingsAsync(gameSettingsStore);
        TextCatalog textCatalog = TextCatalogLoader.Load(gameSettings.EffectiveLanguage);
        textCatalog.MakeCurrent();

        Task prepareTask = ClientPlatform.PrepareAsync is { } prepareAsync ? Task.Run(prepareAsync) : Task.CompletedTask;
        string? processPath = Environment.ProcessPath;
        ClientVersion clientVersion = ClientVersion.Get();
        UpdateClient updateClient = cleanupList.Add(new UpdateClient(clientVersion, AppDirectories.Downloads, log));
        Task<UpdateInfo?> updateGetLatestTask = ClientPlatform.ChecksForUpdates ? Task.Run(updateClient.TryGetLatestAsync) : Task.FromResult<UpdateInfo?>(null);
        UpdateInstaller updateInstaller = new(AppContext.BaseDirectory);
        Task updateCleanupTask = ClientPlatform.ChecksForUpdates ? Task.Run(updateInstaller.Cleanup) : Task.CompletedTask;

        Task<AudioOutput> audioOutputTask = Task.Run(() => AudioOutput.CreateAsync(log));
        GameSettingsSource gameSettingsSource = new(gameSettings);
        Task<AssetProvider> assetProviderTask = Task.Run(() => AssetProvider.LoadDefault(gameSettingsSource, memoryInfo));
        AudioOutput audioOutput = cleanupList.Add(await audioOutputTask);
        AssetProvider assetProvider = cleanupList.Add(await assetProviderTask);

        CheckAssetPack(assetProvider);
        await prepareTask;

        try
        {
            await updateCleanupTask;
        }
        catch (Exception ex)
        {
            log.Error("Failed to clean up updates.", ex);
        }

        FreeThreadedRandomProvider random = new();
        GameLoader gameLoader = new(random);
        GameSaveStore gameSaveStore = new(AppDirectories.Saves, gameSettings.AutoSaveLimit);
        LeaderStore leaderStore = new(AppDirectories.Leaders);
        AssetPreloader assetPreloader = new(assetProvider);
        AudioPlayer audioPlayer = new(assetProvider, audioOutput, log, gameSettings.SoundVolumeRatio, gameSettings.MusicVolumeRatio);
        GameViewModel gameViewModel = cleanupList.Add(new GameViewModel(
            host.Lifetime,
            assetProvider,
            assetPreloader,
            audioPlayer,
            gameLoader,
            gameSaveStore,
            leaderStore,
            gameSettingsStore,
            gameSettingsSource,
            clientVersion,
            log,
            random,
            TaskScheduler.Default));

        Control gameView = host.ShowGame(gameViewModel, gameSettings);

        AssetScope.SetAssets(gameView, assetProvider);
        AudioScope.SetAudio(gameView, audioPlayer);

        currentGame = gameViewModel;
        await gameViewModel.ShowMainMenuCommand.ExecuteAsync(null);

        UpdateInfo? latestUpdate = await updateGetLatestTask;

        if (latestUpdate is not null && latestUpdate.Version > clientVersion)
        {
            if (gameViewModel.TopScreen is MainMenuScreenViewModel)
            {
                gameViewModel.OpenScreen(new UpdatePromptViewModel(
                    latestUpdate,
                    clientVersion,
                    updateClient,
                    updateInstaller,
                    () => RestartProcess(host, processPath),
                    log));
            }
        }
    }

    private void Restart(IGameHost host)
    {
        cleanupList = new CleanupList();
        currentGameException = null;
        TryStartGameAsync(host).FileAndForget();
    }

    private static void RestartProcess(IGameHost host, string? processPath)
    {
        if (processPath is not null)
        {
            string[] args = host.Lifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.Args ?? [] : [];
            using Process process = Process.Start(processPath, args);
        }

        host.Shutdown();
    }

    private static void CheckAssetPack(IAssetMetadataProvider assets)
    {
        AssetSpriteMetadata metadata = assets.GetRequiredSpriteMetadata(new AssetSpriteKey.MainMenuBackground());

        if (metadata.PixelScale == 1)
        {
            throw new InstalledAssetPackNotEnhancedException();
        }
    }

    private async Task<GameSettings> LoadSettingsAsync(GameSettingsStore gameSettingsStore)
    {
        try
        {
            return await gameSettingsStore.TryLoadAsync() ?? GameSettings.Default;
        }
        catch (Exception ex) when (ex is GameSettingsInvalidException or GameSettingsUnsupportedFormatException)
        {
            log.Error("Failed to load the settings.", ex);

            return GameSettings.Default;
        }
    }

    private void OnUnhandledException(IGameHost host, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        log.Error("Unhandled exception.", e.Exception);

        if (currentGameException is null)
        {
            currentGameException = e.Exception;

            try
            {
                if (currentGame is not null)
                {
                    ErrorScreenViewModel errorScreen = new(
                        Tr("An unexpected error occurred.\n{0}", e.Exception.Message),
                        Tr("Exit"),
                        host.Shutdown,
                        log.Location);

                    currentGame.OpenScreen(errorScreen);
                }
                else
                {
                    host.ShowError(new FatalErrorView(Tr("An unexpected error occurred"), e.Exception.Message, log.Location));
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to show error details.", ex);

                host.Shutdown();
            }
        }
    }

    private void HandleStartupException(IGameHost host, Exception ex)
    {
        Control errorView;

        if (ex is InstalledAssetPackException assetPackException)
        {
            string gameDirectory = Path.TrimEndingDirectorySeparator(ClientPlatform.AssetPackDirectory ?? AppContext.BaseDirectory);
            AssetPackErrorViewModel errorViewModel = new(assetPackException, gameDirectory, host.Lifetime, () => Restart(host));

            errorView = new AssetPackErrorView
            {
                DataContext = errorViewModel
            };
        }
        else
        {
            errorView = new FatalErrorView(Tr("Failed to start the game"), ex.Message, log.Location);
        }

        host.ShowError(errorView);
    }
}
