namespace SilverAudioPlayer.Shared.Metadata;
/// <summary>
/// A picture/image
/// </summary>
public interface IPicture : IDisposable
{
    /// <summary>
    /// Textual description of image
    /// </summary>
    public string? Description { get; }
    /// <summary>
    /// Contents of said picture
    /// </summary>
    public WrappedStream? Data { get;  }
    /// <summary>
    /// Type of picture
    /// </summary>
    public PictureType? PicType { get;  }
    /// <summary>
    /// Implementation specific hash (avoid using to compare with image hashes of other file formats)
    /// </summary>
    public string? Hash { get;  }
}