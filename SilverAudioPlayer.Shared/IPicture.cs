namespace SilverAudioPlayer.Shared;

public interface IPicture : IDisposable
{
    public string? Description { get;  }
    public WrappedStream? Data { get;  }
    public PictureType? PicType { get;  }
    public string? Hash { get;  }
}