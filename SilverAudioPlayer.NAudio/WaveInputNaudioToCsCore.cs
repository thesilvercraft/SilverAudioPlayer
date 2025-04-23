#if CSCore
using SilverCraft.CSCore;
using NAudio.Wave;
using WaveFormat = NAudio.Wave.WaveFormat;
using SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class WaveInputNaudioToCsCore(IWaveProvider source) : IWaveInput
{
    public int Read(byte[] buffer, int offset, int count)
    {
        return source.Read(buffer, offset, count);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

   
    public void Dispose()
    {
        if(source is IDisposable disposable) disposable.Dispose();
    }

    public bool CanSeek => source is WaveStream or IWaveStream;

    SilverCraft.CSCore.WaveFormat IAudioSource.WaveFormat => WaveFormatHelper.FromNAudioCore( source.WaveFormat);

    long GetPos()
    {
        return source switch
        {
            WaveStream s => s.Position,
            IWaveStream ws => ws.Position,
            _ => 0
        };
    }

    void SetPos(long position)
    {
        switch (source)
        {
            case WaveStream s:
                s.Position = position;
                break;
            case IWaveStream ws:
                ws.Position = position;
                break;
        }
    }
    long GetLength()
    {
        return source switch
        {
            WaveStream s => s.Length,
            IWaveStream ws => ws.Length,
            _ => 0
        };
    }
    public long Position { get => GetPos(); set=>SetPos(value); }
    public long Length=> GetLength(); 
}
#endif