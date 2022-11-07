using ATL;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class ATLCOREPicture : Picture
{
    public readonly PictureInfo info;

    public ATLCOREPicture(PictureInfo i)
    {
        info = i;
        Data = i.PictureData;
        Description = i.Description;
        MimeType = i.MimeType;
        PicType = (PictureType?)i.PicType;
        Hash = i.PictureHash.ToString();
    }
}