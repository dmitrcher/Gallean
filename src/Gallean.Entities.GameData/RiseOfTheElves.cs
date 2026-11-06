namespace Gallean.Entities.GameData;

public static class RiseOfTheElves
{
    private const string RootDirectoryName = "RiseOfTheElves";
    private const string GlobalsDirectoryName = "Globals";
    private const string ScenariosDirectoryName = "Scens";
    private const string CampaignsDirectoryName = "Campaign";
    private const string LanguagesDirectoryName = "Languages";

    public const string DirectoryName = RootDirectoryName;

    /// <summary>
    /// The folder with the game data. Platforms that cannot ship it next to the executable (such as Android)
    /// extract it elsewhere and point here.
    /// </summary>
    public static string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, RootDirectoryName);
    public static string GlobalDatabasePath => Path.Combine(RootPath, GlobalsDirectoryName);
    public static string ScenarioDatabasePath => Path.Combine(RootPath, ScenariosDirectoryName);
    public static string CampaignDatabasePath => Path.Combine(RootPath, CampaignsDirectoryName);

    public static string GetLanguageGlobalDatabasePath(string languageCode)
    {
        return Path.Combine(RootPath, LanguagesDirectoryName, languageCode, GlobalsDirectoryName);
    }

    public static string GetLanguageScenarioDatabasePath(string languageCode)
    {
        return Path.Combine(RootPath, LanguagesDirectoryName, languageCode, ScenariosDirectoryName);
    }
}
