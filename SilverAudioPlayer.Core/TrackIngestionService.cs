using Microsoft.Extensions.DependencyInjection;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Core;

public class TrackIngestionService
{
    private readonly QueueManager _queueManager;
    private readonly PluginRegistry _pluginRegistry;
    private readonly Func<ILogic> _getLogic;
    private readonly Func<IFullPlayerEnvironment> _getEnvironment;
    private readonly Func<IServiceProvider> _getServiceProvider;

    public TrackIngestionService(
        QueueManager queueManager,
        PluginRegistry pluginRegistry,
        Func<ILogic> getLogic,
        Func<IFullPlayerEnvironment> getEnvironment,
        Func<IServiceProvider> getServiceProvider)
    {
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
        _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));
        _getLogic = getLogic ?? throw new ArgumentNullException(nameof(getLogic));
        _getEnvironment = getEnvironment ?? throw new ArgumentNullException(nameof(getEnvironment));
        _getServiceProvider = getServiceProvider ?? throw new ArgumentNullException(nameof(getServiceProvider));
    }

    public IPlaylist? TryReadPlaylist(WrappedStream playlistStream)
    {
        var playlistReader = _pluginRegistry.PlaylistReaders
            .FirstOrDefault(x => x.SupportedMimes.Contains(playlistStream.MimeType));
        return playlistReader != null && playlistReader.TryParse(playlistStream, out var playlist) ? playlist : null;
    }

    public void ProcessFile(string file, bool oneFile)
    {
        if (_pluginRegistry.SupportedPlaylistMimes().Item2.Any(file.EndsWith))
        {
            var sp = _getServiceProvider();
            using var playlistStream = file.StartsWith("http")
                ? (WrappedStream)new WrappedHttpStream(file, sp.GetRequiredService<HttpClient>())
                : new WrappedFileStream(file);
            var playlist = TryReadPlaylist(playlistStream);
            if (playlist == null) return;
            foreach (var song in playlist.Tracks)
            {
                foreach (var source in song.TrackLoadInfos)
                {
                    AddWithURLBad(source.URL, source.Source == "http", false);
                    break;
                }
            }
        }
        else
        {
            AddWithURLBad(file, file.StartsWith("http"), oneFile);
        }
    }

    public void AddWithURLBad(string f, bool http, bool oneFile)
    {
        var env = _getEnvironment();
        var sp = _getServiceProvider();
        var psps = _pluginRegistry.PlayStreamProviders.Where(x =>
            x is IPlayStreamProviderThatSupportsUrls y && y.IsUrlSupported(new(f), env));

        if (psps.Any())
        {
            Task.Run(async () =>
                await ((IPlayStreamProviderThatSupportsUrls)psps.First()).LoadUrlAsync(new(f), env));
        }
        else
        {
            if (!http)
            {
                if (File.Exists(f))
                {
                    _queueManager.AddSong(new Song(new WrappedFileStream(f), f, logic: _getLogic()), !oneFile);
                }
            }
            else
            {
                _queueManager.AddSong(
                    new Song(new WrappedHttpStream(f, sp.GetRequiredService<HttpClient>()),
                        f, logic: _getLogic()), !oneFile);
            }
        }
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        var enumerable = files?.ToList();
        if (enumerable == null || enumerable.Count == 0) return;

        var oneFile = enumerable.Count == 1;
        if (oneFile && Directory.Exists(enumerable.First()))
        {
            enumerable = FilterFiles(Directory.GetFiles(enumerable.First(), "*", SearchOption.AllDirectories)).ToList();
            oneFile = false;
        }

        enumerable = FilterFiles(enumerable).ToList();
        foreach (var path in enumerable)
        {
            if (Directory.Exists(path))
            {
                var files2 = FilterFiles(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
                foreach (var file in files2)
                {
                    ProcessFile(file, oneFile);
                }
            }
            else
            {
                ProcessFile(path, oneFile);
            }
        }

        _queueManager.SuggestDoingSort();
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Count == 0) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var file in wrappedStreams)
        {
            _queueManager.AddSong(new Song(file, "unknown", logic: _getLogic()), true);
        }

        if (oneFile) return;
        _queueManager.SuggestDoingSort();
    }

    public void LoadSong(WrappedStream stream)
    {
        LoadSong(stream, null);
    }

    public void LoadSong(WrappedStream stream, IMetadata? additionalMetadata)
    {
        _queueManager.AddSong(additionalMetadata != null
            ? new Song(stream, "unknown", logic: _getLogic(), metadata: additionalMetadata)
            : new Song(stream, "unknown", logic: _getLogic()));
    }

    public void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var wrappedStreams = streams.ToList();
        if (wrappedStreams.Count == 0) return;
        var oneFile = wrappedStreams.Count == 1;
        foreach (var (file, meta) in wrappedStreams)
        {
            _queueManager.AddSong(meta != null
                ? new Song(file, "unknown", logic: _getLogic(), metadata: meta)
                : new Song(file, "unknown", logic: _getLogic()), true);
        }

        if (oneFile) return;
        _queueManager.SuggestDoingSort();
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return files.Where(x => !(
            x == null ||
            x.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".docx", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".md", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".7z", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".rar", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".toml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".nfo", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".html", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".xmp", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".log", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".gif", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".cue", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".fpl", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".htm", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".pkf", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".db", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".webp", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".spotdl-cache", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".lrc", StringComparison.InvariantCultureIgnoreCase)
            || x.EndsWith(".accurip", StringComparison.InvariantCultureIgnoreCase)
        ));
    }
}
