using NAudio.Wave;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public static class WaveStreamExtensions
{
    public static void SetPosition(this WaveStream strm, long position) => 
        strm.Position = Math.Max(0, Math.Min(strm.Length, position - position % strm.WaveFormat.BlockAlign));

    public static void SetPosition(this WaveStream strm, double seconds) => strm.SetPosition((long)(seconds * strm.WaveFormat.AverageBytesPerSecond));

    public static void SetPosition(this WaveStream strm, TimeSpan time) => strm.SetPosition(time.TotalSeconds);

    public static void Seek(this WaveStream strm, double offset) => strm.SetPosition(strm.Position + (long)(offset * strm.WaveFormat.AverageBytesPerSecond));
}