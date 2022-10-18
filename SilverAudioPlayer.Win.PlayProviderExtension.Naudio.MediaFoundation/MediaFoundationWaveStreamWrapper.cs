using NAudio.MediaFoundation;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.Naudio.MediaFoundation
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class MediaFoundationWaveStreamWrapper : INaudioWaveStreamWrapper
    {
        public static bool mfinit = false;
        public MediaFoundationWaveStreamWrapper()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && !mfinit)
            {
                MediaFoundationApi.Startup();
                mfinit = true;
            }
        }

        public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType>() { KnownMimes.WAVMime, KnownMimes.AACMime, KnownMimes.MP3Mime };

        public byte GetPlayingAbility(WrappedStream stream)
        {
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (stream is WrappedFileStream && (stream.MimeType == KnownMimes.WAVMime || stream.MimeType == KnownMimes.AACMime || stream.MimeType == KnownMimes.MP3Mime))
                {
                    return 60;
                }
                if (stream.MimeType == KnownMimes.FLACMime && Environment.OSVersion.Version.Major >= 10)
                {
                    return 30;
                }
            }
            
            return 0;
        }

        public WaveStream GetStream(WrappedStream stream)
        {
            if(stream is WrappedFileStream fs)
            {
                try
                {
                    return new MediaFoundationReader(fs.URL);
                }
                catch (COMException)
                {
                    //
                }
            }
            return null;
        }
    }
}