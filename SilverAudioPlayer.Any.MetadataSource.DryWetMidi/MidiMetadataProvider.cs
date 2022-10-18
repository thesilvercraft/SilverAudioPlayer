using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Composition;

namespace SilverAudioPlayer.Any.MetadataSource.DryWetMidi
{
    [Export(typeof(IMetadataProvider))]
    public class MidiMetadataProvider : IMetadataProvider
    {
        public string Name => "DryWetMidi Metadata Provider";

        public string Description => "Metaprovider that provides MIDI metadata";

        public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(MidiMetadataProvider).Assembly, "SilverAudioPlayer.Any.MetadataSource.DryWetMidi.DryWetMidiLogo.png");

        public Version? Version => typeof(MidiMetadataProvider).Assembly.GetName().Version;

        public string Licenses => "GPL";

        public List<Tuple<Uri, URLType>>? Links => new() {
            new(new("https://github.com/thesilvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Any.MetadataSource.DryWetMidi"), URLType.Code),
            new(new($"https://www.nuget.org/packages/Melanchall.DryWetMidi/{typeof(OutputDevice).Assembly.GetName().Version}"), URLType.PackageManager),
            new(new("https://github.com/melanchall/drywetmidi"),URLType.LibraryCode)
        };

        public bool CanGetMetadata(WrappedStream stream) => stream.MimeType == KnownMimes.MidMime;

        public Task<Metadata?> GetMetadata(WrappedStream stream)
        {
            return Task.FromResult((Metadata?)new MidiMetadata(MidiFile.Read(stream.GetStream())));
        }
    }

}