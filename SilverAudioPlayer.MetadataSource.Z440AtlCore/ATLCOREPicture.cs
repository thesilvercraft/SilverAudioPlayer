using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class ATLCOREPicture : IPicture
{
    public  PictureInfo info;

    public ATLCOREPicture(PictureInfo i)
    {
        info = i;
    }

    public void Dispose()
    {
        info = null;
    }

    public string? Description => info.Description;
    public string? MimeType => info.MimeType;
    public byte[]? Data => info.PictureData;
    public ulong Position { get; }
    public PictureType? PicType => (PictureType?)info.PicType;
    public string? Hash => info.PictureHash.ToString();
}