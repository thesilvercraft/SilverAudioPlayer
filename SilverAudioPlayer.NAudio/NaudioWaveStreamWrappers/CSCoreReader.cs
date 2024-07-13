#if CSCore
using SilverCraft.CSCore;
using NAudio.Wave;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public class CSCoreReader(IWaveSource reader) : WaveStream
{
    public IWaveSource _reader = reader;

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _reader.Read(buffer, offset, count);
    }

    public override WaveFormat WaveFormat => WaveFormatHelper.FromCSCore(_reader.WaveFormat);
    public override long Length => _reader.Length;
    public override long Position
    {
        get => _reader.Position;
        set => _reader.Position = value;
    }
}
#endif
