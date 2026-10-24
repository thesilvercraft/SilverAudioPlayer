using SilverCraft.CSCore;
using NAudio.Wave;
using Serilog;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

public class CSCoreReader(IWaveSource reader) : IWaveStream, IDisposable
{
    public IWaveSource _reader = reader;
    private bool _isDisposed;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_isDisposed) return 0;
        try
        {
            return _reader.Read(buffer, offset, count);
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
        catch (ArgumentException)
        {
            return 0;
        }
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
        if (_isDisposed) return;
        _isDisposed = true;
        Log.Debug("Disposing CSCoreReader for {ReaderType}", _reader?.GetType().FullName);
        try
        {
            _reader?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing CSCoreReader reader");
        }
        GC.SuppressFinalize(this);
    }
}
public interface IWaveStream : IWaveProvider, IDisposable
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
