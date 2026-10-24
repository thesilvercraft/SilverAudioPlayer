using FuzzySharp;
using Serilog;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Core;

public class QueueSorter
{
    private readonly object _sortLock = new();
    private CancellationTokenSource? _sortCancellationTokenSource;

    public void SuggestDoingSort(Func<List<Song>> getQueueSnapshot, Action<List<Song>> applySortedQueue, ILogger? log = null)
    {
        lock (_sortLock)
        {
            _sortCancellationTokenSource?.Cancel();
            _sortCancellationTokenSource = new CancellationTokenSource();

            var token = _sortCancellationTokenSource.Token;
            Task.Run(async () => await DoSortAsync(getQueueSnapshot, applySortedQueue, token, log), token);
        }
    }

    public async Task DoSortAsync(Func<List<Song>> getQueueSnapshot, Action<List<Song>> applySortedQueue,
        CancellationToken cancellationToken, ILogger? log = null)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            var elementsCopy = getQueueSnapshot();
            var albums = elementsCopy.GroupBy(a => a.Metadata?.Album);
            List<Tuple<string?, List<Song>>> fuzzedAlbums = [];

            foreach (var album in albums)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var matchedGroup = fuzzedAlbums.Find(x => Fuzz.Ratio(x.Item1 ?? "", album.Key ?? "") > 80);
                if (matchedGroup != null)
                {
                    matchedGroup.Item2.AddRange(album.ToList());
                }
                else
                {
                    fuzzedAlbums.Add(new Tuple<string?, List<Song>>(album.Key, album.ToList()));
                }
            }

            List<Song> sortedSongs = [];
            foreach (var disc in fuzzedAlbums.Select(album => album.Item2.GroupBy(a => a?.Metadata?.DiscNumber ?? 0))
                         .SelectMany(discs => discs.OrderBy(a => a.Key)))
            {
                if (cancellationToken.IsCancellationRequested) return;
                sortedSongs.AddRange(disc.OrderBy(a => a?.Metadata?.TrackNumber ?? int.MaxValue));
            }

            if (cancellationToken.IsCancellationRequested) return;

            applySortedQueue(sortedSongs);
            log?.Information("Sorted through {Count} songs dynamically.", sortedSongs.Count);
        }
        catch (TaskCanceledException)
        {
            // Expected bubble cleanup trace handler
        }
        catch (Exception ex)
        {
            log?.Error(ex, "Error during queue sorting");
        }
    }
}
