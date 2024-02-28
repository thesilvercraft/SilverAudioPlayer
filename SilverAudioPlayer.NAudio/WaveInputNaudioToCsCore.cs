using CSCore;
using NAudio.Wave;
using WaveFormat = NAudio.Wave.WaveFormat;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class WaveInputNaudioToCsCore : IWaveInput
{
    private IWaveProvider _source;
    public WaveInputNaudioToCsCore(IWaveProvider source)
    {
        _source = source;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _source.Read(buffer, offset, count);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

   
    public void Dispose()
    {
    }

    public bool CanSeek => _source is WaveStream;

    CSCore.WaveFormat IAudioSource.WaveFormat => WaveFormatHelper.FromNAudioCore( _source.WaveFormat);

    long GetPos()
    {
        if (_source is WaveStream s)
        {
            return s.Position;
        }

        return 0;
    }

    void SetPos(long position)
    {
        if (_source is WaveStream s)
        {
            s.Position = position;
        }

    }
    long GetLength()
    {
        if (_source is WaveStream s)
        {
            return s.Length;
        }

        return 0;
    }

    public long Position { get => GetPos(); set=>SetPos(value); }
    public long Length=> GetLength(); 
}