namespace Gallean.Configuration;

public static class AppDirectories
{
    private static string LocalApplicationData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Root { get; private set; } = Path.Combine(LocalApplicationData, "Gallean");
    public static string Assets => Path.Combine(Root, "Assets");
    public static string Downloads => Path.Combine(Root, "Downloads");
    public static string EnhanceCache => Path.Combine(Root, "Enhance", "Cache");
    public static string EnhanceModels => Path.Combine(Root, "Enhance", "Models");
    public static string Libraries => Path.Combine(Root, "Libraries");
    public static string Logs => Path.Combine(Root, "Logs");
    public static string OriginalAssets => Path.Combine(Root, "OriginalAssets");
    public static string Saves => Path.Combine(Root, "Saves");

    /// <summary>
    /// Moves all application data to another folder. Platforms without a user-visible local application data
    /// folder (such as Android) call this before anything reads the directories.
    /// </summary>
    public static void UseRoot(string root)
    {
        Root = root;
    }
}
