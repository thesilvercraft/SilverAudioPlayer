using Serilog;
using SilverAudioPlayer.Shared;
using Swordfish.NET.Collections;

namespace SilverAudioPlayer.Core;

public class QueueManager
{
    private readonly PlayerContext _playerContext;
    private readonly QueueSorter _queueSorter;
    private readonly ILogger? _log;
    private long _queueVersion;

    public Song? NextSong { get; set; }

    public Action<Action> DoQueueManipulationAction { get; set; } = a => a();

    public bool AutoSortEnabled
    {
        get => !_queueSorter.IsDisabled;
        set => _queueSorter.IsDisabled = !value;
    }

    public Func<List<Song>>? GetQueueSnapshot { get; set; }

    public event Action<Song?>? NextSongChanged;
    public event Action? QueueModified;

    public QueueManager(PlayerContext playerContext, QueueSorter queueSorter, ILogger? log = null)
    {
        _playerContext = playerContext ?? throw new ArgumentNullException(nameof(playerContext));
        _queueSorter = queueSorter ?? throw new ArgumentNullException(nameof(queueSorter));
        _log = log;

        ConcurrentObservableCollection<Song> defaultQueue = [];
        _playerContext.GetQueue ??= () => defaultQueue;
    }

    public IList<Song> Queue => _playerContext.Queue;

    public Song? CurrentSong
    {
        get => _playerContext.CurrentSong;
        set => _playerContext.CurrentSong = value;
    }

    public Song? GetNextSongInQueue()
    {
        if (CurrentSong == null) return null;
        lock (Queue)
        {
            var index = Queue.IndexOf(CurrentSong);
            if (index < 0) return null;
            if (index + 1 < Queue.Count)
            {
                return Queue[index + 1];
            }

            if (_playerContext.LoopType == RepeatState.Queue && Queue.Count > 0)
            {
                return Queue[0];
            }

            return null;
        }
    }

    public void UpdateNextSongReference()
    {
        if (CurrentSong == null)
        {
            NextSong = null;
            NextSongChanged?.Invoke(null);
            return;
        }

        lock (Queue)
        {
            var index = Queue.IndexOf(CurrentSong);
            if (index >= 0)
            {
                if (index + 1 < Queue.Count)
                {
                    NextSong = Queue[index + 1];
                }
                else if (_playerContext.LoopType == RepeatState.Queue && Queue.Count > 0)
                {
                    NextSong = Queue[0];
                }
                else
                {
                    NextSong = null;
                }
            }
            else
            {
                NextSong = null;
            }

            NextSongChanged?.Invoke(NextSong);
        }
    }

    public void AddSong(Song song, bool expectMore = false)
    {
        DoQueueManipulationAction(() =>
        {
            lock (Queue)
            {
                Queue.Add(song);
                _queueVersion++;
            }

            QueueModified?.Invoke();
        });

        if (expectMore) return;
        SuggestDoingSort();
    }

    public void ReorderQueue(int sourceIndex, int targetIndex, Action<Song?>? onNextSongChanged = null)
    {
        DoQueueManipulationAction(() => ReorderQueueCore(sourceIndex, targetIndex, onNextSongChanged));
    }

    private void ReorderQueueCore(int sourceIndex, int targetIndex, Action<Song?>? onNextSongChanged)
    {
        lock (Queue)
        {
            if (sourceIndex < 0 || sourceIndex >= Queue.Count) return;
            targetIndex = Math.Clamp(targetIndex, 0, Queue.Count);
            if (sourceIndex == targetIndex || sourceIndex + 1 == targetIndex) return;

            var song = Queue[sourceIndex];
            Queue.RemoveAt(sourceIndex);
            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }

            Queue.Insert(targetIndex, song);
            _queueVersion++;

            if (CurrentSong != null)
            {
                var oldNext = NextSong;
                UpdateNextSongReference();
                if (!ReferenceEquals(oldNext, NextSong))
                {
                    onNextSongChanged?.Invoke(NextSong);
                }
            }

            QueueModified?.Invoke();
        }
    }

    public void RemoveSong(Song song, Action? onActiveTrackRemoved = null)
    {
        ArgumentNullException.ThrowIfNull(song, nameof(song));
        DoQueueManipulationAction(() => RemoveSongCore(song, onActiveTrackRemoved));
    }

    private void RemoveSongCore(Song song, Action? onActiveTrackRemoved)
    {
        var isCurrent = ReferenceEquals(CurrentSong, song);
        if (isCurrent)
        {
            _log?.Information("Removing the currently playing track. Halting engine audio pipeline first.");
            onActiveTrackRemoved?.Invoke();
            CurrentSong = null;
        }

        lock (Queue)
        {
            var index = Queue.IndexOf(song);
            if (index == -1) return;

            Queue.RemoveAt(index);
            _queueVersion++;
        }

        song.Dispose();

        UpdateNextSongReference();
        QueueModified?.Invoke();
    }

    public void RemoveSongs(IReadOnlyList<Song?> selectedItems, Action? onActiveTrackRemoved = null)
    {
        foreach (var song in selectedItems.ToList())
        {
            if (song != null)
            {
                RemoveSong(song, onActiveTrackRemoved);
            }
        }
    }

    public void ClearAll()
    {
        DoQueueManipulationAction(() =>
        {
            List<Song> tracksToRemove;
            lock (Queue)
            {
                tracksToRemove = Queue.Where(track => !ReferenceEquals(CurrentSong, track)).ToList();
                foreach (var track in tracksToRemove)
                {
                    Queue.Remove(track);
                }

                if (tracksToRemove.Count > 0)
                {
                    _queueVersion++;
                }
            }

            foreach (var track in tracksToRemove)
            {
                track.Dispose();
            }

            UpdateNextSongReference();
            _log?.Information("Queue cleared. Preserved currently playing track.");
            QueueModified?.Invoke();
        });
    }

    public List<Song> GetQueueCopy()
    {
        if (GetQueueSnapshot is { } snapshot) return snapshot();
        lock (Queue)
        {
            return Queue.ToList();
        }
    }

    public (long Version, List<Song> Songs) GetQueueCopyWithVersion()
    {
        lock (Queue)
        {
            var list = GetQueueSnapshot is { } snapshot ? snapshot() : Queue.ToList();
            return (_queueVersion, list);
        }
    }

    public bool ApplySortedQueue(long expectedVersion, List<Song> sortedSongs)
    {
        var applied = false;
        DoQueueManipulationAction(() =>
        {
            lock (Queue)
            {
                if (_queueVersion != expectedVersion)
                {
                    _log?.Debug("Skipping stale queue sort result (expected version {Expected}, actual {Actual})",
                        expectedVersion, _queueVersion);
                    return;
                }

                Queue.Clear();
                foreach (var song in sortedSongs)
                {
                    Queue.Add(song);
                }

                _queueVersion++;
                applied = true;
            }

            UpdateNextSongReference();
            QueueModified?.Invoke();
        });
        return applied;
    }

    public void SuggestDoingSort()
    {
        long capturedVersion = 0;
        _queueSorter.SuggestDoingSort(
            getQueueSnapshot: () =>
            {
                var (version, songs) = GetQueueCopyWithVersion();
                capturedVersion = version;
                return songs;
            },
            applySortedQueue: sortedSongs =>
            {
                if (!ApplySortedQueue(capturedVersion, sortedSongs))
                {
                    SuggestDoingSort();
                }
            },
            log: _log
        );
    }
}