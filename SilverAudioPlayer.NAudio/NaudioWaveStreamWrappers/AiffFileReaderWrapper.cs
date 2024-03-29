﻿using System.Composition;
using NAudio.Wave;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;

namespace SilverAudioPlayer.NAudio.NaudioWaveStreamWrappers;

[Export(typeof(INaudioWaveStreamWrapper))]
public class AiffFileReaderWrapper : INaudioWaveStreamWrapper
{
    public IReadOnlyList<MimeType> SupportedMimeTypes => new List<MimeType> { KnownMimes.AiffMime };

    public byte GetPlayingAbility(WrappedStream stream)
    {
        if (stream.MimeType == KnownMimes.AiffMime) return 40;
        return 0;
    }

    public WaveStream GetStream(WrappedStream stream)
    {
        return new AiffFileReader(stream.GetStream());
    }
}