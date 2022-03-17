using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SilverAudioPlayer.NAudio
{
    public class NaudioWaveStreamWrapper
    {
        public Type waveType;
        public string[] FileTypes;

        public NaudioWaveStreamWrapper(Type wavetp)
        {
            waveType = wavetp;
        }

        public WaveStream WaveStream(string param)
        {
            ConstructorInfo ctor = waveType.GetConstructor(new[] { typeof(string) });
            return (WaveStream)ctor.Invoke(new object[] { param });
        }
    }

    public static class NaudioWaveStreamWrapperTypes
    {
        private static List<NaudioWaveStreamWrapper> wrapper = new List<NaudioWaveStreamWrapper>()
        {
            new(typeof(WaveFileReader)) { FileTypes = new[] { ".wav" }},
            new(typeof(Mp3FileReader)) { FileTypes = new[] { ".mp3" }},
            new(typeof(AiffFileReader)) { FileTypes = new[] { ".aiff", ".aif" } }
        };

        public static bool HasWrapper(string filetype)
        {
            return wrapper.Any(x => x.FileTypes.Contains(filetype));
        }

        public static NaudioWaveStreamWrapper? GetWrapper(string filetype)
        {
            return wrapper.FirstOrDefault(x => x.FileTypes.Contains(filetype));
        }
    }
}