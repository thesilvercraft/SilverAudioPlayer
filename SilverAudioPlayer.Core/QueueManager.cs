using Avalonia.Collections;
using Avalonia.Threading;
using Serilog;
using SilverAudioPlayer.Shared;
using Swordfish.NET.Collections;

namespace SilverAudioPlayer.Core;

public class QueueManager
{
    private readonly PlayerContext _playerContext;
    private readonly QueueSorter _queueSorter;
    private readonly ILogger? _log;

    public Song? NextSong { get; set; }
    public Action<Action> DoQueueManipulationAction { get; set; } = a => Task.Run(a);

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
            if (index < 0 || index + 1 >= Queue.Count) return null;
            return Queue[index + 1];
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
            NextSong = index >= 0 && index + 1 < Queue.Count ? Queue[index + 1] : null;
            NextSongChanged?.Invoke(NextSong);
        }
    }

    public void AddSong(Song song, bool expectMore = false)
    {
        Queue.Add(song);
        QueueModified?.Invoke();

        if (expectMore) return;
        SuggestDoingSort();
    }

    public void ReorderQueue(int sourceIndex, int targetIndex, Action<Song?>? onNextSongChanged = null)
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
        var index = Queue.IndexOf(song);
        if (index == -1) return;

        // 1. Handle NextSong reference shifting
        if (song.Equals(NextSong))
        {
            NextSong = (index + 1 < Queue.Count) ? Queue[index + 1] : null;
            NextSongChanged?.Invoke(NextSong);
        }

        // 2. Safely teardown if it's the active track
        if (CurrentSong?.Equals(song) == true)
        {
            _log?.Information("Removing the currently playing track. Halting engine audio pipeline first.");
            onActiveTrackRemoved?.Invoke();
            CurrentSong = null;
            song.Dispose();
        }
        else
        {
            song.Dispose();
        }

        Queue.RemoveAt(index);
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
        lock (Queue)
        {
            var tracksToRemove = Queue
                .Where(track => !ReferenceEquals(CurrentSong, track))
                .ToList();

            DoQueueManipulationAction(() =>
            {
                lock (Queue)
                {
                    foreach (var track in tracksToRemove)
                    {
                        Queue.Remove(track);
                        track.Dispose();
                    }
                }

                _log?.Information("Queue cleared. Preserved currently playing track.");
                QueueModified?.Invoke();
            });
        }
    }

    public List<Song> GetQueueCopy()
    {
        List<Song> result = [];
        if (Queue is AvaloniaList<Song>)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                result = [.. Queue];
            }
            else
            {
                Dispatcher.UIThread.Invoke(() => result = [.. Queue]);
            }
        }
        else
        {
            result = new List<Song>(Queue);
        }

        return result;
    }

    public void SuggestDoingSort()
    {
        _queueSorter.SuggestDoingSort(
            getQueueSnapshot: () =>
            {
                lock (Queue)
                {
                    return Queue.ToList();
                }
            },
            applySortedQueue: sortedSongs =>
            {
                DoQueueManipulationAction(() =>
                {
                    lock (Queue)
                    {
                        Queue.Clear();
                        foreach (var song in sortedSongs)
                        {
                            Queue.Add(song);
                        }
                    }
                    UpdateNextSongReference();
                    QueueModified?.Invoke();
                });
            },
            log: _log
        );
    }
}
