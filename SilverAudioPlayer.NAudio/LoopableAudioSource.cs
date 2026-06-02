using NAudio.Wave;
using SilverAudioPlayer.Any.PlayProvider.NAudio.NaudioWaveStreamWrappers;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class LoopableAudioSource : ILoop, IWaveStream, IDisposable
{
    private readonly ISampleStreamBridge _bridge;
    private readonly long _start;
    private readonly long _end;

    public bool CanLoop { get; } = true;
    public bool IsLooping { get; set; } = true;

    public LoopableAudioSource(WaveStream waveStream, long start, long end)
    {
        ArgumentNullException.ThrowIfNull(waveStream);
        _bridge = new NaudioBridge(waveStream);
        _start = start * waveStream.WaveFormat.BlockAlign;
        _end = end * waveStream.WaveFormat.BlockAlign;
    }

    public LoopableAudioSource(IWaveStream waveStream, long start, long end)
    {
        ArgumentNullException.ThrowIfNull(waveStream);
        _bridge = new CscoreBridge(waveStream);
        _start = start * waveStream.WaveFormat.BlockAlign;
        _end = end * waveStream.WaveFormat.BlockAlign;
    }

    public void EnableLoop() => IsLooping = true;
    public void DisableLoop() => IsLooping = false;

    public int Read(byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            int bytesRequired = count - totalBytesRead;

            if (IsLooping)
            {
                long bytesUntilEnd = _end - _bridge.Position;

                if (bytesUntilEnd <= 0)
                {
                    _bridge.Position = _start;
                    bytesUntilEnd = _end - _start;
                }

                if (bytesRequired > bytesUntilEnd)
                {
                    bytesRequired = (int)bytesUntilEnd;
                }
            }

            int bytesRead = _bridge.Read(buffer, offset + totalBytesRead, bytesRequired);

            if (bytesRead == 0)
            {
                if (IsLooping)
                {
                    _bridge.Position = _start;
                    continue;
                }
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    public WaveFormat WaveFormat => _bridge.WaveFormat;

    #region Stream Bridges

    private interface ISampleStreamBridge : IDisposable
    {
        long Position { get; set; }
        long Length { get; }
        WaveFormat WaveFormat { get; }
        int Read(byte[] buffer, int offset, int count);
    }

    private class NaudioBridge : ISampleStreamBridge
    {
        private readonly WaveStream _stream;
        public NaudioBridge(WaveStream stream) => _stream = stream;

        public long Position { get => _stream.Position; set => _stream.Position = value; }
        public long Length { get => _stream.Length;  }
        public WaveFormat WaveFormat => _stream.WaveFormat;
        public int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public void Dispose()
        {
            _stream.Dispose();
        }
    }

    private class CscoreBridge : ISampleStreamBridge
    {
        private readonly IWaveStream _stream;
        public CscoreBridge(IWaveStream stream) => _stream = stream;

        public long Position { get => _stream.Position; set => _stream.Position = value; }
        public long Length { get => _stream.Length;  }
        public WaveFormat WaveFormat =>_stream.WaveFormat;

        public int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public void Dispose()
        {
            _stream.Dispose();
        }
    }

    #endregion

    public void Dispose()
    {
         _bridge.Dispose();
    }

    public long Position { get => _bridge.Position; set => _bridge.Position = value; }
    public long Length { get => _bridge.Length;  }
}