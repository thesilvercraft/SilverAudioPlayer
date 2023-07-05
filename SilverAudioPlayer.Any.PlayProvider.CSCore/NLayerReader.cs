#if FFMPEG
#else 
using CSCore;
using NLayer;

namespace SilverAudioPlayer.Any.PlayProvider.CSCore;

public class NLayerReader : IWaveSource
{
    private MpegFile _mpegFile;
    public NLayerReader(Stream s)
    {
        _mpegFile=new MpegFile(s);
    }
    public int Read(byte[] buffer, int offset, int count) => _mpegFile.ReadSamples(buffer, offset, count);

    public void Dispose()
    {
        _mpegFile.Dispose();
        GC.SuppressFinalize(this);
    }

    public bool CanSeek => _mpegFile.CanSeek;
    public WaveFormat WaveFormat => new(_mpegFile.SampleRate, 32, _mpegFile.Channels, AudioEncoding.IeeeFloat);
    public long Position { get=>_mpegFile.Position; set=>_mpegFile.Position=value; }
    public long Length => _mpegFile.Length;
}
#endif