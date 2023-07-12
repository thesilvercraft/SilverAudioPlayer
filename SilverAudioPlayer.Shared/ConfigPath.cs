namespace SilverAudioPlayer.Shared;

public static class ConfigPath
{
    public static string BasePath { get; internal set; }
    /// <summary>
    /// DO NOT USE THIS METHOD UNLESS YOU'RE IMPLEMENTING YOUR OWN UI FOR SILVERAUDIOPLAYER
    /// </summary>
    /// <param name="base">the base configuration path</param>
    public static void Set(string @base)
    {
        BasePath = @base;
    }

    public static string GetPath(string subpath)
    {
        return Path.Combine(BasePath, subpath);
    }
}