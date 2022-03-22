using NAudio.Flac;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using System.ComponentModel.Composition;

namespace SilverAudioPlayer.Naudio.Flac
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class FlacNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
    {
        public bool CanPlay(string file)
        {
            if (File.Exists(file))
            {
                using var bytes = File.OpenRead(file);
                byte[] vs = new byte[4];
                return bytes.Read(vs, 0, 4) == 4
                    && vs[0] == 0x66 && vs[1] == 0x4C && vs[2] == 0x61 && vs[3] == 0x43;
            }
            return false;
        }

        public WaveStream GetStream(string file)
        {
            return new FlacReader(file);
        }
    }
}