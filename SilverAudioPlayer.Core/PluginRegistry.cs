using System.Composition;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;

namespace SilverAudioPlayer.Core;

public class PluginRegistry : ILogicImportable
{
    [ImportMany] public IEnumerable<IPlayProvider> PlayProviders { get; set; } = [];
    [ImportMany] public IEnumerable<IMetadataProvider> MetadataProviders { get; set; } = [];
    [ImportMany] public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; } = [];
    [ImportMany] public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; } = [];
    [ImportMany] public IEnumerable<IPlayStreamProvider> PlayStreamProviders { get; set; } = [];
    [ImportMany] public IEnumerable<ISyncPlugin> SyncPlugins { get; set; } = [];
    [ImportMany] public IEnumerable<IPlaylistReader> PlaylistReaders { get; set; } = [];
    [ImportMany] public IEnumerable<IAdditionalMetadataGatherer> MetadataGatherers { get; set; } = [];
    [Import] public IWillProvideMemory? MemoryProvider { get; set; }

    public List<MimeType> PlayableMimes { get; set; } = [];

    public Func<string, bool> CheckIfPluginAllowed { get; set; } = _ => true;

    public Func<IEnumerable<IPlayProvider>, WrappedStream, Task<IPlayProvider?>> ChoosePlayProvider { get; set; } =
        (providers, _) => Task.FromResult(providers.FirstOrDefault());

    public IEnumerable<ICodeInformation> PluginCodeInformation =>
    [
        .. PlayProviders,
        .. MusicStatusInterfaces,
        .. MetadataProviders,
        .. WakeLockInterfaces,
        .. PlayStreamProviders,
        .. PlaylistReaders,
        .. SyncPlugins,
        .. MetadataGatherers
    ];

    public async Task<IPlay?> GetPlayerFromStreamAsync(WrappedStream stream, IFullPlayerEnvironment environment)
    {
        var matchedProviders = PlayProviders.Where(x => x.CanPlayFile(stream));
        var chosen = await ChoosePlayProvider(matchedProviders, stream);
        return chosen?.GetPlayer(stream, environment);
    }

    public async Task<IMetadata?> GetMetadataFromStreamAsync(WrappedStream stream,
        IEnumerable<IMetadata?>? additionalMetadata = null, ILogger? log = null)
    {
        try
        {
            var listOfMetadatas = new List<IMetadata>();
            foreach (var provider in MetadataProviders.Where(x => x.CanGetMetadata(stream)))
            {
                try
                {
                    var meta = await provider.GetMetadata(stream);
                    if (meta != null)
                    {
                        listOfMetadatas.Add(meta);
                    }
                }
                catch (Exception ex)
                {
                    log?.Error(ex, "Provider {Provider} failed extracting metadata", provider.GetType().Name);
                }
            }

            if (additionalMetadata != null)
            {
                listOfMetadatas.AddRange(additionalMetadata.Where(x => x is not null)!);
            }

            if (listOfMetadatas.Count == 0 && !MetadataGatherers.Any())
            {
                return null;
            }

            return new MetadataCombo(listOfMetadatas, MetadataGatherers);
        }
        catch (Exception e)
        {
            log?.Error(e, "Error loading metadata from stream");
            return null;
        }
    }

    public static Tuple<List<string>, List<string>> SupportedTrackMimesG(IEnumerable<MimeType> supportedMimes)
    {
        var enumerable = supportedMimes.ToList();
        var mimeTypes = enumerable.Select(x => x.Common).ToList();
        mimeTypes.AddRange(enumerable.SelectMany(y => y.AlternativeTypes));
        var fileExt = enumerable.SelectMany(x => x.FileExtensions).ToList();
        return new(mimeTypes, fileExt);
    }

    public Tuple<List<string>, List<string>> SupportedTrackMimes()
    {
        return SupportedTrackMimesG(PlayableMimes);
    }

    public Tuple<List<string>, List<string>> SupportedPlaylistMimes()
    {
        var playlistMimes = PlaylistReaders.SelectMany(x => x.SupportedMimes).Distinct();
        return SupportedTrackMimesG(playlistMimes);
    }

    public Tuple<List<string>, List<string>> SupportedTrackAndPlaylistMimes()
    {
        var (a, b) = SupportedTrackMimesG(PlayableMimes);
        var (c, d) = SupportedPlaylistMimes();
        a.AddRange(c);
        b.AddRange(d);
        return new(a.Distinct().ToList(), b.Distinct().ToList());
    }
}