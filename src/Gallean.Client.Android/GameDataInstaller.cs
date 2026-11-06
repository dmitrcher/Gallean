using System.IO.Compression;
using Android.Content;
using Android.Content.PM;

namespace Gallean.Client.Android;

/// <summary>
/// Unpacks the game data archive from the APK into app storage once per installed build.
/// </summary>
internal static class GameDataInstaller
{
    private const string ArchiveName = "RiseOfTheElves.zip";
    private const string StampFileName = ".installed";

    public static void Install(Context context, string targetDirectory)
    {
        string stamp = GetBuildStamp(context);
        string stampPath = Path.Combine(targetDirectory, StampFileName);

        if (File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
        {
            return;
        }

        string temporaryDirectory = targetDirectory + ".tmp";

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        using (Stream archive = context.Assets!.Open(ArchiveName))
        {
            ZipFile.ExtractToDirectory(archive, temporaryDirectory, overwriteFiles: true);
        }

        File.WriteAllText(Path.Combine(temporaryDirectory, StampFileName), stamp);

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.Move(temporaryDirectory, targetDirectory);
    }

    private static string GetBuildStamp(Context context)
    {
        PackageInfo info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;

        return $"{info.LongVersionCode}:{info.LastUpdateTime}";
    }
}
