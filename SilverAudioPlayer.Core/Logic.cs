using FuzzySharp;
using ReactiveUI;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using System.Collections.ObjectModel;
using System.Composition;

namespace SilverAudioPlayer.Core
{
    public class PlayerContext : ReactiveObject
    {
        public Action<byte> VolumeChanged;
        public Func<byte> GetVolume;
        public Func<RepeatState> GetLoopType;
        public Action<RepeatState> SetLoopType;
        public Action ResetUIScrollBar;
        public Action HandleLateStageMetadataAndScrollBar;

        public Action<TimeSpan> SetScrollBarTextTo;

        public byte Volume { get => GetVolume(); set => VolumeChanged(value); }
        public byte _Volume=50;
        public ObservableCollection<Song> Queue { get => _queue; set => this.RaiseAndSetIfChanged(ref _queue, value); }
        private ObservableCollection<Song> _queue = new();
        public Song? CurrentSong { get => _CurrentSong; set => this.RaiseAndSetIfChanged(ref _CurrentSong, value); }
        private Song? _CurrentSong = null;

        public RepeatState LoopType { get => GetLoopType(); set => SetLoopType(value); }
        public RepeatState _LoopType;
    }

    public class Logic<T> where T : PlayerContext
    {
        public Logic(T playercontext)
        {
            playerContext = playercontext;
            playerContext.VolumeChanged = (vol) =>
            {
                Player?.SetVolume(vol);
                playerContext.RaiseAndSetIfChanged(ref playerContext._Volume, vol);
            };
            playerContext.GetVolume = () => playerContext._Volume;
            playerContext.GetLoopType = () => playerContext._LoopType;
            playerContext.SetLoopType = (lt) => playerContext.RaiseAndSetIfChanged(ref playerContext._LoopType, lt);
        }
        [ImportMany]
        public IEnumerable<IPlayProvider> PlayProviders { get; set; }

        [ImportMany]
        public IEnumerable<IMetadataProvider> MetadataProviders { get; set; }
        [ImportMany]
        public IEnumerable<IMusicStatusInterface> MusicStatusInterfaces { get; set; }
        [ImportMany]
        public IEnumerable<IWakeLockProvider> WakeLockInterfaces { get; set; }
        public Serilog.Core.Logger log { get; set; }
        public T playerContext { get; set; }
        public List<MimeType> PlayableMimes { get; set; }

        private byte Volume { get => playerContext.Volume; set => playerContext.Volume = value; }

        public IPlay? Player { get; set; }
        public Song? CurrentSong { get => playerContext.CurrentSong; set => playerContext.CurrentSong = value; }

        public bool StopAutoLoading = false;
        public Song? NextSong = null;
        private CancellationTokenSource? token = new();
        public readonly List<IMusicStatusInterface> musicStatusInterfaces = new();

        public void AddMSI(IMusicStatusInterface e)
        {
            musicStatusInterfaces.Add(e);
            e.Play += MusicStatusInterface_Play;
            e.Pause += MusicStatusInterface_Pause;
            e.Stop += MusicStatusInterface_Stop;
            e.PlayPause += MusicStatusInterface_PlayPause;
            e.Next += MusicStatusInterface_Next;
            e.Previous += MusicStatusInterface_Previous;
            e.GetCurrentTrack += MusicStatusInterface_GetCurrentTrack;
            e.GetDuration += MusicStatusInterface_GetDuration;
            e.GetPosition += MusicStatusInterface_GetPosition;
            e.SetPosition += MusicStatusInterface_SetPosition;
            e.GetShuffle += MusicStatusInterface_GetShuffle;
            e.GetState += MusicStatusInterface_GetState;
            e.GetVolume += MusicStatusInterface_GetVolume;
            e.SetVolume += MusicStatusInterface_SetVolume;
            e.GetRepeat += MusicStatusInterface_GetRepeat;
            e.SetRating += MusicStatusInterface_SetRating;
            e.SetRepeat += MusicStatusInterface_SetRepeat;
            e.StartIPC();
        }

        private void MusicStatusInterface_SetRepeat(object? sender, RepeatState e)
        {
            playerContext.LoopType = e;
        }

        private void MusicStatusInterface_SetVolume(object? sender, byte e)
        {
            if (e <= 100)
            {
                Player?.SetVolume(e);
            }
        }

        private void MusicStatusInterface_SetPosition(object? sender, ulong e)
        {
            Player?.SetPosition(TimeSpan.FromSeconds(e));
        }

        private void MusicStatusInterface_SetRating(object? sender, byte e)
        {
            //TODO eventually
        }

        private bool MusicStatusInterface_GetShuffle()
        {
            return false;
        }

        private ulong MusicStatusInterface_GetPosition()
        {
            return (ulong)(Player?.GetPosition().TotalSeconds ?? 1);
        }

        private RepeatState MusicStatusInterface_GetRepeat()
        {
            return playerContext.LoopType;
        }

        private byte MusicStatusInterface_GetVolume()
        {
            return Volume;
        }

        private PlaybackState MusicStatusInterface_GetState()
        {
            return Player?.GetPlaybackState() ?? PlaybackState.Stopped;
        }

        private void MusicStatusInterface_Stop(object? sender, object e)
        {
            StopAutoLoading = true;
            Player?.Stop();
            SendIfStateIsNotNull();
        }

        private void MusicStatusInterface_Pause(object? sender, object e)
        {
            Player?.Pause();
            SendIfStateIsNotNull();
        }

        private void SendIfStateIsNotNull()
        {
            var state = Player?.GetPlaybackState();
            if (state != null)
            {
                PlaybackStateChangedNotification(state.Value);
            }
            else
            {
                PlaybackStateChangedNotification(PlaybackState.Stopped);
            }
        }
        /// <summary>
        /// Lets music status interfaces know about a track change
        /// </summary>
        /// <param name="currentSong">The new track</param>
        public void TrackChangedNotification(Song? currentSong)
        {
            Parallel.ForEach(musicStatusInterfaces, msI => msI?.TrackChangedNotification(currentSong!));
        }
        /// <summary>
        /// Lets music status interfaces know about a playstate change
        /// </summary>
        /// <param name="s">The new Playstate</param>
        public void PlaybackStateChangedNotification(PlaybackState s)
        {
            Parallel.ForEach(musicStatusInterfaces, msI => msI?.PlayerStateChanged(s));
            if (s == PlaybackState.Stopped)
            {
                Parallel.ForEach(WakeLockInterfaces, msI => msI.UnWakeLock());
            }
            else if (s == PlaybackState.Playing)
            {
                Parallel.ForEach(WakeLockInterfaces, msI => msI.WakeLock());
            }
        }

        private void MusicStatusInterface_Play(object? sender, object e)
        {
            Play();
        }

        public void RemoveMSI(IMusicStatusInterface e)
        {
            e.Play -= MusicStatusInterface_Play;
            e.Pause -= MusicStatusInterface_Pause;
            e.Stop -= MusicStatusInterface_Stop;
            e.Next -= MusicStatusInterface_Next;
            e.Previous -= MusicStatusInterface_Previous;
            e.GetCurrentTrack -= MusicStatusInterface_GetCurrentTrack;
            e.GetDuration -= MusicStatusInterface_GetDuration;
            e.GetState -= MusicStatusInterface_GetState;
            e.GetVolume -= MusicStatusInterface_GetVolume;
            e.GetRepeat -= MusicStatusInterface_GetRepeat;
            e.GetPosition -= MusicStatusInterface_GetPosition;
            e.GetShuffle -= MusicStatusInterface_GetShuffle;
            e.SetRating -= MusicStatusInterface_SetRating;
            e.SetPosition -= MusicStatusInterface_SetPosition;
            e.SetVolume -= MusicStatusInterface_SetVolume;
            e.PlayPause -= MusicStatusInterface_PlayPause;
            e.SetRepeat -= MusicStatusInterface_SetRepeat;
            e.StopIPC();
            e.Dispose();
            musicStatusInterfaces.Remove(e);
        }

        public void PlayPause(bool allowstart)
        {
            if (Player?.GetPlaybackState() == PlaybackState.Playing)
            {
                Pause();
            }
            else if (Player?.GetPlaybackState() == PlaybackState.Paused)
            {
                Play();
            }
            else if (allowstart)
            {
                Play();
            }
        }

        private void MusicStatusInterface_PlayPause(object? sender, object e)
        {
            PlayPause(true);
        }

        private ulong MusicStatusInterface_GetDuration()
        {
            return (ulong?)(Player?.Length()?.TotalSeconds) ?? (ulong?)(CurrentSong?.Metadata?.Duration / 1000) ?? 2;
        }

        private Song MusicStatusInterface_GetCurrentTrack()
        {
            return CurrentSong;
        }

        private void MusicStatusInterface_Previous(object? sender, EventArgs e)
        {
            Previous();
        }

        private void MusicStatusInterface_Next(object? sender, EventArgs e)
        {
            Next();
        }
        bool ChangeAllowed = true;
        private void Next()
        {
            if (ChangeAllowed)
            {
                var a = playerContext.Queue.IndexOf(CurrentSong);
                if (a != -1 && a + 1 < playerContext.Queue.Count)
                {
                    ChangeAllowed = false;
                    HandleSongChanging(playerContext.Queue[a + 1], true);
                    ChangeAllowed = true;
                }
            }
        }

        private void Previous()
        {
            if (ChangeAllowed)
            {
                var a = playerContext.Queue.IndexOf(CurrentSong);
                if (a != -1 && a - 1 >= 0)
                {
                    ChangeAllowed = false;
                    HandleSongChanging(playerContext.Queue[a - 1], true);
                    ChangeAllowed = true;
                }
            }
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            if (MusicStatusInterfaces?.Any() == true)
            {
                Parallel.ForEach(MusicStatusInterfaces, dangthing =>
                {
                    var a = dangthing;
                    GC.KeepAlive(a);
                    AddMSI(a);
                });
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopAutoLoading = true;
            Parallel.ForEach(MusicStatusInterfaces.ToArray(), dangthing => RemoveMSI(dangthing));
            if (Player != null)
            {
                Player.TrackEnd -= OutputDevice_PlaybackStopped;
            }
            StopAutoLoading = true;
            Player?.Stop();
            Player = null;
            Environment.Exit(0);
        }

        public void Play()
        {
            if (Player != null)
            {
                Player?.Play();
                SendIfStateIsNotNull();
            }
            else
            {
                StartPlaying();
            }
        }

        public void Pause()
        {
            Player?.Pause();
            SendIfStateIsNotNull();
        }

        [TimingAdvice]
        public void StartPlaying(bool play = true, bool resetsal = false)
        {
            if (CurrentSong == null)
            {
                if (playerContext.Queue.Count > 0)
                {
                    CurrentSong = playerContext.Queue[0];
                }
                else
                {
                    Log.Information("Avoiding fatal crash, nothing to play");
                    return;
                }
            }
            Player = GetPlayerFromStream(CurrentSong.Stream);

            if (Player == null)
            {
                //TODO MSGBOX
                //var window = new MessageBox("Error", "I do not know how to play " + CurrentSong.URI);
                // window.ShowDialog(this);
                throw new NotSupportedException("aaaa");
                return;
            }

            Log.Information("Got player of type {PlayerType}", Player.GetType());

            Task.Run(() => TrackChangedNotification(CurrentSong));
            if (play)
            {
                Player.SetVolume(Volume);
                Player.Play();
                SendIfStateIsNotNull();
                Player.TrackEnd += OutputDevice_PlaybackStopped;
                playerContext.ResetUIScrollBar();
              
                if (Player?.Length() is TimeSpan totalusable)
                {
                playerContext.SetScrollBarTextTo(totalusable);
                }
                playerContext.HandleLateStageMetadataAndScrollBar();

                }
                if (resetsal)
                {
                    StopAutoLoading = false;
                }
        }

        [TimingAdvice]
        public void RemoveTrack()
        {
            Player?.Stop();
            Player = null;
            token?.Cancel();
            Thread.Sleep(30);
        }

        [TimingAdvice]
        public void OutputDevice_PlaybackStopped(object? sender, object o)
        {
            Log.Information(@"Output device playback stopped
StopAutoLoading: {StopAutoLoading}
Current song: {CurrentSong}
Loop mode: {LoopType}", StopAutoLoading, CurrentSong, playerContext.LoopType);

            if (playerContext.LoopType == RepeatState.One && !StopAutoLoading)
            {
                //Loop
                StartPlaying();
            }
            else if (CurrentSong != null && !StopAutoLoading)
            {
                var a = playerContext.Queue.IndexOf(CurrentSong);
                if (a != -1)
                {
                    if (a + 1 < playerContext.Queue.Count)
                    {
                        HandleSongChanging(playerContext.Queue[a + 1], true);
                    }
                    else if (playerContext.LoopType == RepeatState.Queue)
                    {
                        HandleSongChanging(playerContext.Queue[0], true);
                    }
                    else
                    {
                        Log.Information("RemoveTrack in PlaybackStopped");
                        RemoveTrack();
                    }
                }
                else if (NextSong != null)
                {
                    HandleSongChanging(NextSong, true);
                    NextSong = null;
                }
            }
            StopAutoLoading = false;
        }
        [TimingAdvice]
        public void HandleSongChanging(Song NextSong, bool resetsal = false)
        {
            Log.Information("StopAutoLoading set to true in HandleSongChanging");
            StopAutoLoading = true;
            /*var curr = playerContext.Queue.IndexOf(CurrentSong);
            var next = playerContext.Queue.IndexOf(NextSong);
            if (next == -1)
            {
                Log.Information("!!!!!!! NEXT is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong}\nNextSong.URI is {NextSongURI}\nCurrentSong.Guid is {CurrentSong}\nCurrentSong.URI is {CurrentSongURI}", NextSong.Guid, NextSong.URI, CurrentSong?.Guid, CurrentSong?.URI);
            }
            else
            {*/
                CurrentSong = NextSong;
                RemoveTrack();
                StartPlaying(resetsal: resetsal);
            /*}
            if (curr == -1)
            {
                Log.Information("!!!!!!! CURR is NULL in HSC !!!!!!!!\nNextSong.Guid is {NextSong}\nNextSong.URI is {NextSongURI}\nCurrentSong.Guid is {CurrentSong}\nCurrentSong.URI is {CurrentSongURI}", NextSong.Guid, NextSong.URI, CurrentSong?.Guid, CurrentSong?.URI);
            }*/
        }

        public void ProcessFiles(IEnumerable<string> files)
        {
            if (files?.Any() == true)
            {
                bool onefile = files.Count() == 1;
                if (onefile && Directory.Exists(files.First()))
                {
                    files = Directory.GetFiles(files.First());
                    onefile = false;
                }
                files = FilterFiles(files);
                foreach (var file in files)
                {
                    AddSong(new Song(file, file, Guid.NewGuid()), !onefile);
                }
                if (!onefile && !IsSortRequested)
                {
                    IsSortRequested = true;
                    Task.Run(() => DoSort());
                }
            }
        }

        public void ProcessStreams(IEnumerable<WrappedStream> streams)
        {
            if (streams?.Any() == true)
            {
                bool onefile = streams.Count() == 1;
                foreach (var file in streams)
                {
                    AddSong(new Song(file, "unknown", Guid.NewGuid()), true);
                }
                if (!onefile && !IsSortRequested)
                {
                    IsSortRequested = true;
                    Task.Run(() => DoSort());
                }
            }
        }

        public void ProcessStream(WrappedStream stream)
        {
            if (stream != null)
            {
                AddSong(new Song(stream, "unknown", Guid.NewGuid()));
            }
        }

        public void ClearAll()
        {
            playerContext.Queue.Clear();
        }

        private void AddSong(Song song, bool expectmore = false)
        {
            Task.Run(async () =>
            {
                song.Metadata ??= await GetMetadataFromStream(song.Stream)!;
                playerContext.Queue.Add(song);
                if (!expectmore && !IsSortRequested)
                {
                    IsSortRequested = true;
                    Task sortTask = Task.Run(() => DoSort());
                }
            });
        }
        bool IsSortRequested = false;

        [TimingAdvice]
        public async Task DoSort()
        {
            await Task.Delay(200);
            List<Song> sng = new();
            IEnumerable<IGrouping<string?, Song>> albums = playerContext.Queue.AsEnumerable().GroupBy(a => a.Metadata.Album);
            List<Tuple<string?, List<Song>>> fuzzedAlbums = new();
            foreach (var album in albums)
            {
                if (fuzzedAlbums.Find(x => Fuzz.Ratio(x.Item1 ?? "", album.Key ?? "") > 80) is Tuple<string?, List<Song>> group)
                {
                    group.Item2.AddRange(album.ToList());
                }
                else
                {
                    fuzzedAlbums.Add(new(album.Key, album.ToList()));
                }
            }
            foreach (var album in fuzzedAlbums)
            {
                var discs = album.Item2.GroupBy(a => a?.Metadata?.DiscNumber ?? 0);
                foreach (var disc in discs.OrderBy(a => a.Key))
                {
                    sng.AddRange(disc.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
                }
            }
            Log.Information("Sorted through {Count} songs", sng.Count);
            playerContext.Queue = new ObservableCollection<Song>(sng);
            IsSortRequested = false;
        }
        public IPlay? GetPlayerFromStream(WrappedStream stream)
        {
            var provider = PlayProviders?.FirstOrDefault(x => x.CanPlayFile(stream));
            return provider?.GetPlayer(stream);
        }

        public IMetadataProvider? GetMetadataProviderFromStream(WrappedStream stream)
        {
            return MetadataProviders?.FirstOrDefault(x => x.CanGetMetadata(stream));
        }

        public Task<Metadata?>? GetMetadataFromStream(WrappedStream stream)
        {
            return GetMetadataProviderFromStream(stream)?.GetMetadata(stream);
        }
        public IEnumerable<string> FilterFiles(IEnumerable<string> files) => files.Where(x => !(
           x.EndsWith(".png")
        || x.EndsWith(".txt")
        || x.EndsWith(".docx")
        || x.EndsWith(".pdf")
        || x.EndsWith(".csv")
        || x.EndsWith(".jpg")
        || x.EndsWith(".lnk")
        || x.EndsWith(".md")
        || x.EndsWith(".zip")
        || x.EndsWith(".7z")
        || x.EndsWith(".rar")
        || x.EndsWith(".exe")
        || x.EndsWith(".dll")
        || x.EndsWith(".json")
        || x.EndsWith(".toml")
        || x.EndsWith(".yaml")
        || x.EndsWith(".xml")
        || x.EndsWith(".nfo")
        || x.EndsWith(".html")
        || x.EndsWith(".m3u")
        || x.EndsWith(".xmp")
        || x.EndsWith(".log")
        || x.EndsWith(".gif")
        || x.EndsWith(".cue")
        || x.EndsWith(".m3u")
        || x.EndsWith(".fpl")
        || x.EndsWith(".htm")
        || x.EndsWith(".pkf")
        || x.EndsWith(".db")
        || x.EndsWith(".webp")
        || x.EndsWith(".spotdl-cache")
        ));
    }
}