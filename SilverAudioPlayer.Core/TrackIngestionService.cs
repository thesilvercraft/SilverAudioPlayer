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

    private static bool IsHttpUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private bool IsPlaylistPath(string file)
    {
        var supportedExts = _pluginRegistry.SupportedPlaylistMimes().Item2;
        var pathToCheck = file;
        if (IsHttpUrl(file) && Uri.TryCreate(file, UriKind.Absolute, out var uri))
        {
            pathToCheck = uri.AbsolutePath;
        }

        return supportedExts.Any(ext => pathToCheck.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    public void ProcessFile(string file, bool oneFile)
    {
        if (IsPlaylistPath(file))
        {
            var isHttp = IsHttpUrl(file);
            var sp = _getServiceProvider();
            using var playlistStream = isHttp
                ? (WrappedStream)new WrappedHttpStream(file, sp.GetRequiredService<HttpClient>())
                : new WrappedFileStream(file);
            var playlist = TryReadPlaylist(playlistStream);
            if (playlist == null) return;
            var trackList = playlist.Tracks.ToList();
            var totalTracks = trackList.Count;
            for (var i = 0; i < totalTracks; i++)
            {
                var song = trackList[i];
                var isLast = i == totalTracks - 1;
                foreach (var source in song.TrackLoadInfos)
                {
                    var sourceIsHttp = IsHttpUrl(source.URL);
                    if (!sourceIsHttp && !File.Exists(source.URL))
                    {
                        continue;
                    }

                    AddWithURLBad(source.URL, sourceIsHttp, isLast && oneFile);
                    break;
                }
            }

            if (oneFile)
            {
                _queueManager.SuggestDoingSort();
            }
        }
        else
        {
            AddWithURLBad(file, IsHttpUrl(file), oneFile);
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
            var provider = (IPlayStreamProviderThatSupportsUrls)psps.First();
            _ = Task.Run(async () =>
            {
                try
                {
                    await provider.LoadUrlAsync(new(f), env);
                    _queueManager.SuggestDoingSort();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed loading URL {Url} via {Provider}", f, provider.GetType().Name);
                }
            });
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
        var inputPaths = files?.ToList();
        if (inputPaths == null || inputPaths.Count == 0) return;

        var expandedFiles = new List<string>();
        var containsDirectory = false;
        foreach (var path in inputPaths)
        {
            if (Directory.Exists(path))
            {
                containsDirectory = true;
                expandedFiles.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
            }
            else
            {
                expandedFiles.Add(path);
            }
        }

        var filtered = FilterFiles(expandedFiles).ToList();
        if (filtered.Count == 0) return;

        var oneFile = !containsDirectory && inputPaths.Count == 1 && filtered.Count == 1;
        foreach (var file in filtered)
        {
            ProcessFile(file, oneFile);
        }

        if (!oneFile)
        {
            _queueManager.SuggestDoingSort();
        }
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
        var (mimes, extensions) = _pluginRegistry.SupportedTrackAndPlaylistMimes();
        return files.Where(file =>
        {
            if (extensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase))) return true;
            if (File.Exists(file))
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var matchedMime = SilverMagicBytes.MagicByteCombos.Match(stream, 0)?.MimeType;
                    return matchedMime != null && (mimes.Contains(matchedMime.Common) ||
                                                   matchedMime.AlternativeTypes.Any(mimes.Contains));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        });
    }
}