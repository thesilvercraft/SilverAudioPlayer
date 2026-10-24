using System.Composition;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverMagicBytes;

namespace SilverAudioPlayer.Core;

public interface ILogicImportable
{
    [ImportMany] public IEnumerable<IPlayProvider> PlayProviders { get; set; }
    [ImportMany] public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }
    [ImportMany] public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }
    [ImportMany] public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
    [ImportMany] public IEnumerable<IPlayStreamProvider> PlayStreamProviders { get; set; }
    [ImportMany] public IEnumerable<ISyncPlugin> SyncPlugins { get; set; }
    [ImportMany] public IEnumerable<IPlaylistReader> PlaylistReaders { get; set; }
    [ImportMany] public IEnumerable<IAdditionalMetadataGatherer> MetadataGatherers { get; set; }
    [Import] public IWillProvideMemory? MemoryProvider { get; set; }

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
}

public class Logic<T> : ILogic, ILogicImportable where T : PlayerContext
{
    public PluginRegistry Plugins { get; }
    public QueueManager QueueManager { get; }
    public QueueSorter QueueSorter { get; }
    public TrackIngestionService TrackIngestion { get; }
    public PlaybackEngine PlaybackEngine { get; }
    public MusicStatusCoordinator MusicStatus { get; }
    public ReplayGainHandler ReplayGain { get; }
    public MusicBrainzHandler MusicBrainz { get; }
    public IPlayerUiBridge UiBridge { get; set; }

    public List<IMusicStatusInterface> LoadedMusicStatusInterfaces => MusicStatus.LoadedMusicStatusInterfaces;

    public Song? NextSong
    {
        get => QueueManager.NextSong;
        set => QueueManager.NextSong = value;
    }

    public bool StopAutoLoading
    {
        get => PlaybackEngine.StopAutoLoading;
        set => PlaybackEngine.StopAutoLoading = value;
    }

    public bool PreferAlbumReplayGain
    {
        get => ReplayGain.PreferAlbumReplayGain;
        set => ReplayGain.PreferAlbumReplayGain = value;
    }

    public Action<Action> DoQueueManipulationAction
    {
        get => QueueManager.DoQueueManipulationAction;
        set => QueueManager.DoQueueManipulationAction = value;
    }

    [ImportMany]
    public IEnumerable<IPlayProvider> PlayProviders
    {
        get => Plugins.PlayProviders;
        set => Plugins.PlayProviders = value;
    }

    [ImportMany]
    public IEnumerable<IMetadataProvider> MetadataProviders
    {
        get => Plugins.MetadataProviders;
        set => Plugins.MetadataProviders = value;
    }

    [ImportMany]
    public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces
    {
        get => Plugins.MusicStatusInterfaces;
        set => Plugins.MusicStatusInterfaces = value;
    }

    [ImportMany]
    public IEnumerable<IWakeLockProvider> WakeLockInterfaces
    {
        get => Plugins.WakeLockInterfaces;
        set => Plugins.WakeLockInterfaces = value;
    }

    [ImportMany]
    public IEnumerable<IPlayStreamProvider> PlayStreamProviders
    {
        get => Plugins.PlayStreamProviders;
        set => Plugins.PlayStreamProviders = value;
    }

    [ImportMany]
    public IEnumerable<ISyncPlugin> SyncPlugins
    {
        get => Plugins.SyncPlugins;
        set => Plugins.SyncPlugins = value;
    }

    [ImportMany]
    public IEnumerable<IPlaylistReader> PlaylistReaders
    {
        get => Plugins.PlaylistReaders;
        set => Plugins.PlaylistReaders = value;
    }

    [ImportMany]
    public IEnumerable<IAdditionalMetadataGatherer> MetadataGatherers
    {
        get => Plugins.MetadataGatherers;
        set => Plugins.MetadataGatherers = value;
    }

    [Import]
    public IWillProvideMemory? MemoryProvider
    {
        get => Plugins.MemoryProvider;
        set => Plugins.MemoryProvider = value;
    }

    public Func<IEnumerable<IPlayProvider>, WrappedStream, Task<IPlayProvider?>> ChoosePlayProvider
    {
        get => Plugins.ChoosePlayProvider;
        set => Plugins.ChoosePlayProvider = value;
    }

    public Func<string, bool> CheckIfPluginAllowed
    {
        get => Plugins.CheckIfPluginAllowed;
        set => Plugins.CheckIfPluginAllowed = value;
    }

    public ILogger Log { get; set; } = Serilog.Log.Logger;
    public T PlayerContext { get; set; }

    public List<MimeType> PlayableMimes
    {
        get => Plugins.PlayableMimes;
        set => Plugins.PlayableMimes = value;
    }

    public IFullPlayerEnvironment PlayerEnvironment { get; set; } = null!;

    public IPlay? Player
    {
        get => PlaybackEngine.Player;
        set => PlaybackEngine.Player = value;
    }

    public Logic(T playercontext, IPlayerUiBridge? uiBridge = null)
    {
        PlayerContext = playercontext ?? throw new ArgumentNullException(nameof(playercontext));
        UiBridge = uiBridge ?? new PlayerContextUiBridge(playercontext);

        Plugins = new PluginRegistry();
        QueueSorter = new QueueSorter();
        QueueManager = new QueueManager(PlayerContext, QueueSorter, Log);
        MusicStatus = new MusicStatusCoordinator();
        ReplayGain = new ReplayGainHandler();
        MusicBrainz = new MusicBrainzHandler();

        PlaybackEngine = new PlaybackEngine(
            PlayerContext,
            QueueManager,
            Plugins,
            MusicStatus,
            UiBridge,
            () => PlayerEnvironment,
            Log);

        TrackIngestion = new TrackIngestionService(
            QueueManager,
            Plugins,
            () => this,
            () => PlayerEnvironment,
            () => PlayerContext.ServiceProvider);

        PlayerContext.VolumeChanged ??= vol =>
        {
            PlaybackEngine.SetVolume(vol);
            PlayerContext.RaiseAndSetIfChanged(ref PlayerContext._Volume, vol);
        };

        PlayerContext.GetVolume ??= () => PlayerContext._Volume;
        PlayerContext.GetLoopType ??= () => PlayerContext._LoopType;
        PlayerContext.SetLoopType ??= lt =>
        {
            PlayerContext.RaiseAndSetIfChanged(ref PlayerContext._LoopType, lt);
            PlaybackEngine.SetLoopMode(lt);
        };

        var mediator = PlayerContext.ServiceProvider.GetRequiredService<IMediator>();
        mediator.OnQueryReceived += query => ReplayGain.HandleMediatorQuery(query, PlayerContext.CurrentSong);
        mediator.OnQueryReceived += query => MusicBrainz.HandleMediatorQuery(query, PlayerContext.CurrentSong);
    }

    public void AddMSI(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListenerAdmin logicMusicInterfaceListener)
    {
        MusicStatus.AddMSI(statusInterface, logicMusicInterfaceListener);
    }

    public void RemoveMsi(IMusicStatusInterface statusInterface,
        IMusicStatusInterfaceListener logicMusicInterfaceListener)
    {
        MusicStatus.RemoveMsi(statusInterface, logicMusicInterfaceListener);
    }

    public void TrackChangedNotification(Song? currentSong)
    {
        MusicStatus.TrackChangedNotification(currentSong);
    }

    public void PlaybackStateChangedNotification(PlaybackState s)
    {
        MusicStatus.PlaybackStateChangedNotification(s, Plugins.WakeLockInterfaces);
    }

    public void PlayPause(bool allowStart)
    {
        PlaybackEngine.PlayPause(allowStart);
    }

    public void Next()
    {
        PlaybackEngine.Next();
    }

    public void Previous()
    {
        PlaybackEngine.Previous();
    }

    public void SetPosition(double seconds) => PlaybackEngine.SetPosition(seconds);

    public void SetPosition(TimeSpan pos) => PlaybackEngine.SetPosition(pos);

    public IPlaylist? TryReadPlaylist(WrappedStream playlistStream)
    {
        return TrackIngestion.TryReadPlaylist(playlistStream);
    }

    public void MainWindow_Opened(IMusicStatusInterfaceListenerAdmin musicStatusInterface)
    {
        MusicStatus.MainWindow_Opened(musicStatusInterface, Plugins.MusicStatusInterfaces, Plugins.CheckIfPluginAllowed);
    }

    public void Play()
    {
        PlaybackEngine.Play();
    }

    public void Pause()
    {
        PlaybackEngine.Pause();
    }

    public void StartPlaying(bool play = true, bool resetsal = false)
    {
        PlaybackEngine.StartPlaying(play, resetsal);
    }

    public Task StartPlayingAsync(bool play = true, bool resetsal = false)
    {
        return PlaybackEngine.StartPlayingAsync(play, resetsal);
    }

    public void RemoveTrack()
    {
        PlaybackEngine.RemoveTrack();
    }

    public void OutputDevice_PlaybackStopped(object? sender, object o)
    {
        PlaybackEngine.OutputDevice_PlaybackStopped(sender, o);
    }

    public void HandleSongChanging(Song nextSong)
    {
        PlaybackEngine.HandleSongChanging(nextSong);
    }

    public void AddWithURLBad(string f, bool http, bool oneFile)
    {
        TrackIngestion.AddWithURLBad(f, http, oneFile);
    }

    public void ProcessFiles(IEnumerable<string> files)
    {
        TrackIngestion.ProcessFiles(files);
    }

    public void LoadSongs(IEnumerable<WrappedStream> streams)
    {
        TrackIngestion.LoadSongs(streams);
    }

    public void LoadSong(WrappedStream stream)
    {
        TrackIngestion.LoadSong(stream);
    }

    public void LoadSong(WrappedStream stream, IMetadata? additionalMetadata)
    {
        TrackIngestion.LoadSong(stream, additionalMetadata);
    }

    public void LoadSongs(IEnumerable<Tuple<WrappedStream, IMetadata?>> streams)
    {
        TrackIngestion.LoadSongs(streams);
    }

    public void ClearAll()
    {
        QueueManager.ClearAll();
    }

    public void AddSong(Song song, bool expectmore = false)
    {
        QueueManager.AddSong(song, expectmore);
    }

    public void ReorderQueue(int sourceIndex, int targetIndex)
    {
        QueueManager.ReorderQueue(sourceIndex, targetIndex, nextSong =>
        {
            if (PlaybackEngine.Player is IPlayLancer && nextSong != null)
            {
                if (PlaybackEngine.ReplaceNextStreamForGaplessPlayback(nextSong))
                {
                    Log?.Debug("Replaced queued gapless stream after reorder: {NextSong}", nextSong.URI);
                }
                else
                {
                    PlaybackEngine.TryEnqueueNextSongForGaplessPlayback();
                }
            }
        });
    }

    public void RemoveSong(Song song)
    {
        QueueManager.RemoveSong(song, () => PlaybackEngine.RemoveTrack());
    }

    public void RemoveSongs(IReadOnlyList<Song?> selectedItems)
    {
        QueueManager.RemoveSongs(selectedItems, () => PlaybackEngine.RemoveTrack());
    }

    public Task DoSortAsync(CancellationToken cancellationToken)
    {
        return QueueSorter.DoSortAsync(
            () =>
            {
                lock (QueueManager.Queue)
                {
                    return QueueManager.Queue.ToList();
                }
            },
            sortedSongs =>
            {
                QueueManager.DoQueueManipulationAction(() =>
                {
                    lock (QueueManager.Queue)
                    {
                        QueueManager.Queue.Clear();
                        foreach (var song in sortedSongs)
                        {
                            QueueManager.Queue.Add(song);
                        }
                    }
                    QueueManager.UpdateNextSongReference();
                });
            },
            cancellationToken,
            Log
        );
    }

    public Task<IPlay?> GetPlayerFromStream(WrappedStream stream)
    {
        return Plugins.GetPlayerFromStreamAsync(stream, PlayerEnvironment);
    }

    public Task<IMetadata?>? GetMetadataFromStream(WrappedStream stream,
        IEnumerable<IMetadata?>? additionalMetadata = null)
    {
        return Plugins.GetMetadataFromStreamAsync(stream, additionalMetadata, Log);
    }

    public IEnumerable<string> FilterFiles(IEnumerable<string> files)
    {
        return TrackIngestion.FilterFiles(files);
    }

    public List<Song> GetQueueCopy()
    {
        return QueueManager.GetQueueCopy();
    }

    public Tuple<List<string>, List<string>> SupportedTrackMimes()
    {
        return Plugins.SupportedTrackMimes();
    }

    public Tuple<List<string>, List<string>> SupportedPlaylistMimes()
    {
        return Plugins.SupportedPlaylistMimes();
    }

    public Tuple<List<string>, List<string>> SupportedTrackAndPlaylistMimes()
    {
        return Plugins.SupportedTrackAndPlaylistMimes();
    }
}
