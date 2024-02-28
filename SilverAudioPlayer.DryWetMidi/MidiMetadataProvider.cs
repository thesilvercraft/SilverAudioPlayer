using System.Composition;
using System.Runtime.InteropServices;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using SilverAudioPlayer.Any.PlayProvider.DryWetMidi;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.MetadataSource.DryWetMidi;

[Export(typeof(IMetadataProvider))]
public class MidiMetadataProvider : IMetadataProvider
{
    public string Name => "DryWetMidi Metadata Provider";

    public string Description => "Metadata Provider that provides MIDI metadata";

    public WrappedStream Icon => new WrappedEmbeddedResourceStream(typeof(MidiMetadataProvider).Assembly,
        "SilverAudioPlayer.Any.PlayProviderAndMetadataProvider.DryWetMidi.DryWetMidiLogo.png");

    public Version? Version => typeof(MidiMetadataProvider).Assembly.GetName().Version;

    public string Licenses => "GPL";

    public List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri(
                "https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.MetadataSource.DryWetMidi"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri(
                $"https://www.nuget.org/packages/Melanchall.DryWetMidi/{typeof(OutputDevice).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/melanchall/drywetmidi"), URLType.LibraryCode)
    ];

    public bool CanGetMetadata(WrappedStream stream)
    {
        return stream.MimeType == KnownMimes.MidMime;
    }

    public Task<IMetadata?> GetMetadata(WrappedStream stream)
    {
        return Task.FromResult((IMetadata?)new MidiMetadata(MidiFile.Read(stream.GetStream())));
    }
}