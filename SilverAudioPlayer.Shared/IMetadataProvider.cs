﻿namespace SilverAudioPlayer.Shared;

public interface IMetadataProvider : ICodeInformation
{
    public Task<IMetadata?> GetMetadata(WrappedStream stream);

    public bool CanGetMetadata(WrappedStream stream);
}

public interface IMetadata : IDisposable
{
    public string? Title { get;  }
    public string? Artist { get;  }
    public string? Album { get;  }
    public string? Genre { get;  }
    public int? Year { get;  }
    public ulong? Bitrate { get;  }
    public ulong? SampleRate { get;  }
    public uint? Channels { get;  }

    public int? TrackNumber { get;  }
    public int? DiscNumber { get;  }
    public string[]? Comments { get;  }

    /// <summary>
    ///     duration in milliseconds
    /// </summary>
    public double? Duration { get;  }

    public string? Lyrics { get;  }
    public IList<LyricPhrase>? SyncedLyrics { get;  }
    public IReadOnlyList<IPicture>? Pictures { get;  }
}
public abstract class Metadata :IMetadata
{
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public int? Year { get; init; }
    public ulong? Bitrate { get; init; }
    public ulong? SampleRate { get; init; }
    public uint? Channels { get; init; }

    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string[]? Comments { get; init; }

    /// <summary>
    ///     duration in milliseconds
    /// </summary>
    public double? Duration { get; init; }

    public string? Lyrics { get; init; }
    public IList<LyricPhrase>? SyncedLyrics { get; set; }
    public IReadOnlyList<IPicture>? Pictures { get; init; }
    public abstract void Dispose();
}

public interface IPicture : IDisposable
{
    public string? Description { get;  }
    public WrappedStream? Data { get;  }
    public PictureType? PicType { get;  }
    public string? Hash { get;  }
}
public  class Picture :IPicture
{
    public Picture(WrappedStream stream)
    {
        Cached = SharedMemoryStreamPoolInstance.Instance.GetFromWrappedStream(stream);
        Reliance = new(Cached);
        Hash ??= Cached.Hash;
    }
    public string? Description { get; init; }
    private SharedStream? Cached;
    private RelianceOnSharedStream? Reliance;
   
    public WrappedStream? Data => Cached.Stream;
    public PictureType? PicType { get; set; }
    public string? Hash { get; set; }

    public virtual void Dispose()
    {
        Reliance?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public enum PictureType
{
    //
    // Summary:
    //     Unsupported (i.e. none of the supported values in the enum)
    Unknown = 99,

    //
    // Summary:
    //     Generic
    Generic = 1,

    //
    // Summary:
    //     Front cover
    Front = 2,

    //
    // Summary:
    //     Back cover
    Back = 3,

    //
    // Summary:
    //     CD
    CD = 4,

    //
    // Summary:
    //     File icon
    Icon = 5,

    //
    // Summary:
    //     Leaflet
    Leaflet = 6,

    //
    // Summary:
    //     Lead artist/lead performer/soloist
    LeadArtist = 7,

    //
    // Summary:
    //     Artist/performer
    Artist = 8,

    //
    // Summary:
    //     Conductor
    Conductor = 9,

    //
    // Summary:
    //     Band/Orchestra
    Band = 10,

    //
    // Summary:
    //     Composer
    Composer = 11,

    //
    // Summary:
    //     Lyricist/text writer
    Lyricist = 12,

    //
    // Summary:
    //     Recording location
    RecordingLocation = 13,

    //
    // Summary:
    //     During recording
    DuringRecording = 14,

    //
    // Summary:
    //     During performance
    DuringPerformance = 0xF,

    //
    // Summary:
    //     Movie/video screen capture
    MovieCapture = 0x10,

    //
    // Summary:
    //     Illustration
    Illustration = 18,

    //
    // Summary:
    //     Band/artist logotype
    BandLogo = 19,

    //
    // Summary:
    //     Publisher/Studio logotype
    PublisherLogo = 20
}