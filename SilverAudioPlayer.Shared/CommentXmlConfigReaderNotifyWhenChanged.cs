using SilverConfig;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace SilverAudioPlayer.Shared;

public class CommentXmlConfigReaderNotifyWhenChanged<T> : CommentXmlConfigReader<T>, IDisposable
    where T : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    private readonly List<FileSystemWatcher> _fileSystemWatchers = new();

    public void Dispose()
    {
        foreach (var fsw in _fileSystemWatchers) fsw.Dispose();
        _fileSystemWatchers.Clear();
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
                        base.Write(config, fullPath);
                    }
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    Debug.WriteLine($"[CommentXmlConfigReader] Write contention on attempt {attempt}/{maxRetries} for {fullPath}: {ex.Message}");
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
                Debug.WriteLine($"[CommentXmlConfigReader] Read contention on attempt {attempt}/{maxRetries} for {fullPath}: {ex.Message}");
                Thread.Sleep(baseDelayMs * attempt);
            }
        }

        if (c == null) return default;

        var fpDir = Path.GetDirectoryName(fullPath) ?? "";
        var fpNm = Path.GetFileName(fullPath);

        if (Directory.Exists(fpDir))
        {
            FileSystemWatcher j = new()
            {
                Path = fpDir,
                EnableRaisingEvents = true,
                Filter = fpNm,
                NotifyFilter = NotifyFilters.CreationTime
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size
            };

            j.Changed += (x, y) =>
            {
                if (y.ChangeType != WatcherChangeTypes.Changed) return;
                if (!string.Equals(y.FullPath, fullPath, StringComparison.OrdinalIgnoreCase) || !c.AllowedToRead) return;

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
                        if (a.Name != "AllowedToRead" && a.CanRead && a.GetValue(c)?.Equals(a.GetValue(c2)) != true)
                        {
                            if (a.CanWrite)
                                a.SetValue(c, a.GetValue(c2));
                            else
                                Debug.WriteLine("CommentXmlConfigReaderNotifyWhenChanged had an issue setting c." + a.Name);

                            c.PropertyChanged(this, new PropertyChangedEventArgsFromFile(a.Name));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[CommentXmlConfigReader] Error handling file change: {e}");
                }
            };

            _fileSystemWatchers.Add(j);
        }

        return c;
    }
}
public class PropertyChangedEventArgsFromFile(string? propertyName) : PropertyChangedEventArgs(propertyName)
{
}