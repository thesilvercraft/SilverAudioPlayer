using SilverConfig;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace SilverAudioPlayer.Shared;

internal static class SharedConfigDirectoryWatchers
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static readonly Lock RegistryLock = new();
    private static readonly Dictionary<string, DirectoryWatcherEntry> Watchers = new(PathComparer);
    public static readonly ConcurrentDictionary<string, long> LastSelfWriteTicks = new(PathComparer);

    internal sealed class DirectoryWatcherEntry(string directoryPath, FileSystemWatcher watcher)
    {
        public string DirectoryPath { get; } = directoryPath;
        public FileSystemWatcher Watcher { get; } = watcher;
        public Dictionary<string, List<Subscription>> SubscribersByFile { get; } = new(PathComparer);
        public int TotalSubscribers;
    }

    internal sealed class Subscription : IDisposable
    {
        private readonly DirectoryWatcherEntry _entry;
        private readonly string _fullFilePath;
        private readonly Action _onChanged;
        private int _disposed;

        internal Subscription(DirectoryWatcherEntry entry, string fullFilePath, Action onChanged)
        {
            _entry = entry;
            _fullFilePath = fullFilePath;
            _onChanged = onChanged;
        }

        internal void Invoke() => _onChanged();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            FileSystemWatcher? watcherToDispose = null;
            lock (RegistryLock)
            {
                if (_entry.SubscribersByFile.TryGetValue(_fullFilePath, out var list) && list.Remove(this))
                {
                    _entry.TotalSubscribers--;
                    if (list.Count == 0)
                    {
                        _entry.SubscribersByFile.Remove(_fullFilePath);
                    }

                    if (_entry.TotalSubscribers <= 0 &&
                        Watchers.TryGetValue(_entry.DirectoryPath, out var current) &&
                        ReferenceEquals(current, _entry))
                    {
                        Watchers.Remove(_entry.DirectoryPath);
                        watcherToDispose = _entry.Watcher;
                    }
                }
            }

            watcherToDispose?.Dispose();
        }
    }

    public static IDisposable? Subscribe(string fullFilePath, Action onChanged)
    {
        var dir = Path.GetDirectoryName(fullFilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
        var fullDir = Path.GetFullPath(dir);

        lock (RegistryLock)
        {
            if (!Watchers.TryGetValue(fullDir, out var entry))
            {
                var fsw = new FileSystemWatcher
                {
                    Path = fullDir,
                    Filter = "*",
                    NotifyFilter = NotifyFilters.CreationTime
                                   | NotifyFilters.DirectoryName
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                entry = new DirectoryWatcherEntry(fullDir, fsw);
                fsw.Changed += (_, args) => OnFileChanged(entry, args);
                Watchers[fullDir] = entry;
            }

            if (!entry.SubscribersByFile.TryGetValue(fullFilePath, out var list))
            {
                list = [];
                entry.SubscribersByFile[fullFilePath] = list;
            }

            var sub = new Subscription(entry, fullFilePath, onChanged);
            list.Add(sub);
            entry.TotalSubscribers++;
            return sub;
        }
    }

    private static void OnFileChanged(DirectoryWatcherEntry entry, FileSystemEventArgs args)
    {
        if (args.ChangeType != WatcherChangeTypes.Changed) return;
        string changedPath;
        try
        {
            changedPath = Path.GetFullPath(args.FullPath);
        }
        catch
        {
            return;
        }

        List<Subscription>? snapshot = null;
        lock (RegistryLock)
        {
            if (entry.SubscribersByFile.TryGetValue(changedPath, out var list) && list.Count > 0)
            {
                snapshot = [.. list];
            }
        }

        if (snapshot == null) return;
        foreach (var sub in snapshot)
        {
            sub.Invoke();
        }
    }
}

public class CommentXmlConfigReaderNotifyWhenChanged<T> : CommentXmlConfigReader<T>, IDisposable
    where T : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    private readonly ConcurrentDictionary<string, IDisposable> _subscriptions =
        new(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        foreach (var sub in _subscriptions.Values) sub.Dispose();
        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }

    public static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

    private static object GetLockObject(string fullPath)
    {
        return Locks.GetOrAdd(fullPath, _ => new object());
    }

    public override void Write(T config, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var lockObj = GetLockObject(fullPath);

        void TryWrite()
        {
            const int maxRetries = 10;
            const int baseDelayMs = 25;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    lock (lockObj)
                    {
                        SharedConfigDirectoryWatchers.LastSelfWriteTicks[fullPath] = Stopwatch.GetTimestamp();
                        base.Write(config, fullPath);
                        SharedConfigDirectoryWatchers.LastSelfWriteTicks[fullPath] = Stopwatch.GetTimestamp();
                    }

                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    Debug.WriteLine(
                        $"[CommentXmlConfigReader] Write contention on attempt {attempt}/{maxRetries} for {fullPath}: {ex.Message}");
                    Thread.Sleep(baseDelayMs * attempt);
                }
            }
        }

        if (config is ICanBeToldThatAPartOfMeIsChanged a)
        {
            a.AllowedToRead = false;
            try
            {
                TryWrite();
            }
            finally
            {
                a.AllowedToRead = true;
            }
        }
        else
        {
            TryWrite();
        }
    }

    public override T? Read(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var lockObj = GetLockObject(fullPath);

        T? c = default;
        const int maxRetries = 10;
        const int baseDelayMs = 25;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                lock (lockObj)
                {
                    c = base.Read(fullPath);
                }

                break;
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                Debug.WriteLine(
                    $"[CommentXmlConfigReader] Read contention on attempt {attempt}/{maxRetries} for {fullPath}: {ex.Message}");
                Thread.Sleep(baseDelayMs * attempt);
            }
        }

        if (c == null) return default;

        if (!_subscriptions.ContainsKey(fullPath))
        {
            var subscription = SharedConfigDirectoryWatchers.Subscribe(fullPath, () =>
            {
                if (!c.AllowedToRead) return;

                if (SharedConfigDirectoryWatchers.LastSelfWriteTicks.TryGetValue(fullPath, out var lastWrite) &&
                    Stopwatch.GetElapsedTime(lastWrite) < TimeSpan.FromMilliseconds(250))
                {
                    return;
                }

                try
                {
                    T? c2 = default;
                    for (var attempt = 1; attempt <= 5; attempt++)
                    {
                        try
                        {
                            lock (lockObj)
                            {
                                c2 = base.Read(fullPath);
                            }

                            break;
                        }
                        catch (IOException) when (attempt < 5)
                        {
                            Thread.Sleep(20 * attempt);
                        }
                    }

                    if (c2 == null) return;

                    var t = typeof(T);
                    foreach (var a in t.GetProperties())
                    {
                        if (a.Name != "AllowedToRead" && a.CanRead && !Equals(a.GetValue(c), a.GetValue(c2)))
                        {
                            if (a.CanWrite)
                                a.SetValue(c, a.GetValue(c2));
                            else
                                Debug.WriteLine("CommentXmlConfigReaderNotifyWhenChanged had an issue setting c." +
                                                a.Name);

                            c.PropertyChanged(this, new PropertyChangedEventArgsFromFile(a.Name));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[CommentXmlConfigReader] Error handling file change: {e}");
                }
            });

            if (subscription != null && !_subscriptions.TryAdd(fullPath, subscription))
            {
                subscription.Dispose();
            }
        }

        return c;
    }
}

public class PropertyChangedEventArgsFromFile(string? propertyName) : PropertyChangedEventArgs(propertyName)
{
}