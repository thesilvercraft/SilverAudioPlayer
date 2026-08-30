using System.Runtime.CompilerServices;

namespace SilverAudioPlayer.Shared;

public static class ConstantCodeInfo
{
    public const string GitRepo = "https://gitlab.com/silvercraft/SilverAudioPlayer";

    private static string GetCurrentSourceFolderName(string callerPath)
    {
        return Path.GetFileName(Path.GetDirectoryName(callerPath)) ?? string.Empty;
    }

    public static string GetCurrentUrl([CallerFilePath] string callerPath = "")
    {
        return $"{GitRepo}/-/tree/master/{GetCurrentSourceFolderName(callerPath)}";
    }
}