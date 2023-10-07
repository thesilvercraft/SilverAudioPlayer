using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class AtlCoreSharedInfo
{
    public static readonly List<Tuple<Uri, URLType>>? Links = new()
    {
        new Tuple<Uri, URLType>(
            new Uri(
                "https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.MetadataSource.Z440AtlCore"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/z440.atl.core/{typeof(Track).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/Zeugma440/atldotnet"), URLType.LibraryCode)
    };

}