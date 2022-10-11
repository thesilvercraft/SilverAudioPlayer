﻿using NAudio.Vorbis;
using NAudio.Wave;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.Any.PlayProviderExtenison.Naudio.Vorbis
{
    [Export(typeof(INaudioWaveStreamWrapper))]
    public class VorbisNaudioWaveStreamWrapper : INaudioWaveStreamWrapper
    {
        public byte GetPlayingAbility(WrappedStream stream)
        {
            if (stream.MimeType == KnownMimes.OGGMime)
            {
                return 30;
            }
            return 0;
        }


        public WaveStream GetStream(WrappedStream stream)
        {
            return new VorbisWaveReader(stream.GetStream());
        }
    }
}