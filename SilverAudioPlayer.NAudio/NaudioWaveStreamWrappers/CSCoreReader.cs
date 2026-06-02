using SilverCraft.CSCore;
using NAudio.Wave;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public class CSCoreReader(IWaveSource reader) : IWaveStream,  IDisposable
{
    public IWaveSource _reader = reader;

    public int Read(byte[] buffer, int offset, int count)
    {
        return _reader.Read(buffer, offset, count);
    }

    public WaveFormat WaveFormat => WaveFormatHelper.FromCSCore(_reader.WaveFormat);
    public long Length => _reader.Length;
    public long Position
    {
        get => _reader.Position;
        set => _reader.Position = value;
    }
    public void Dispose()
    {
        _reader.Dispose();
        GC.SuppressFinalize(this);
    }
}
public interface IWaveStream : IWaveProvider,IDisposable
{
    long Position { get; set; }
    long Length { get; }
    public TimeSpan CurrentTime
    {
        get => TimeSpan.FromSeconds((double) Position /  WaveFormat.AverageBytesPerSecond);
        set => Position = (long) (value.TotalSeconds *  WaveFormat.AverageBytesPerSecond);
    }
    public virtual int BlockAlign => this.WaveFormat.BlockAlign;
    public TimeSpan TotalTime => TimeSpan.FromSeconds((double) Length / WaveFormat.AverageBytesPerSecond);
}