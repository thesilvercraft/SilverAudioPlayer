namespace SilverAudioPlayer.Shared;

public static class ConfigPath
{
    public static string BasePath { get; internal set; }

    public static void Set(string @base)
    {
        BasePath = @base;
    }
}