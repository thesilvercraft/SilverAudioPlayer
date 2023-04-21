using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public  class WrappedMemoryStream : WrappedStream, IDisposable
{
    public WrappedMemoryStream(byte[] data, MimeType? mt = null)
    {
        _data = data;
        if (mt == null)
        {
            var stream2 = GetStream();
            try
            {
                mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
            }
            finally
            {
                stream2.Dispose();
                Streams.Remove(stream2);
            }
        }
        MimeType = mt;
    }
    private byte[] _data;
    List<Stream> Streams { get; set; } = new();
    public override MimeType MimeType { get; }
    public override Stream GetStream()
    {
        var stream = new MemoryStream(_data);
        Streams.Add(stream);
        return stream;
    }

    public override bool ShouldDisposeStream => true;
 
    public override void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        foreach (var stream in Streams.Where(x => x.CanRead))
            stream.Dispose();
    }

    ~WrappedMemoryStream()
    {
        Dispose(false);
    }
  
}