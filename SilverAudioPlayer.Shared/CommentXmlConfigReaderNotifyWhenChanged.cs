using SilverConfig;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SilverAudioPlayer.Shared;


public class CommentXmlConfigReaderNotifyWhenChanged<T> : CommentXmlConfigReader<T>, IDisposable
    where T : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    private readonly List<FileSystemWatcher> fileSystemWatchers = new();

    public void Dispose()
    {
        foreach (var fsw in fileSystemWatchers) fsw.Dispose();
        GC.SuppressFinalize(this);
    }
    DateTime? LastWriteTime = DateTime.Now;
    public static ConcurrentDictionary<string, object> Locks = new();
    public override void Write(T config, string path)
    {
        Debug.WriteLine($"Writing config");
        void TryWrite()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || DateTime.Now > LastWriteTime + TimeSpan.FromMilliseconds(100))
            {
                LastWriteTime = DateTime.Now;
                try
                {
                    if (Locks.TryGetValue(path, out var obj))
                    {
                        lock (obj)
                        {
                            base.Write(config, path);
                        }
                    }
                    else
                    {
                        object o = new();
                        Locks[path] = o;
                        lock (o)
                        {
                            base.Write(config, path);
                        }
                    }
                }
                catch (IOException e) when (e.Message.StartsWith("The process cannot access the file ") && e.Message.EndsWith(" because it is being used by another process."))
                {
                    //Try again later, i guess
                }
            }
        }
        if (config is ICanBeToldThatAPartOfMeIsChanged a)
        {
            a.AllowedToRead = false;
            TryWrite();
            a.AllowedToRead = true;
        }
        else
        {
            TryWrite();
        }
    }
    public override T? Read(string path)
    {
        Debug.WriteLine($"Reading config");
        T c;
        if (Locks.TryGetValue(path, out var obj))
        {
            lock (obj)
            {
                c = base.Read(path);
            }
        }
        else
        {
            object o = new();
            Locks[path] = o;
            lock (o)
            {
                c = base.Read(path);
            }
        }
        var fp = Path.GetFullPath(path);
        var fpdir = Path.GetDirectoryName(fp) ?? "";
        var fpnm = Path.GetFileName(fp);

        FileSystemWatcher j = new()
        {
            Path = fpdir,
            EnableRaisingEvents = true,
            Filter = fpnm,
            NotifyFilter = NotifyFilters.CreationTime
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size
        };
        j.Changed += (x, y) =>
        {
            if (y.ChangeType != WatcherChangeTypes.Changed) return;
            if (y.FullPath != fp || !c.AllowedToRead) return;

            try
            {
                Debug.WriteLine($"ChangedRead config");
                T? c2 = base.Read(path);
                var t = typeof(T);
                foreach (var a in t.GetProperties())
                    if (a.Name != "AllowedToRead" && a.CanRead && a.GetValue(c)?.Equals(a.GetValue(c2)) != true)
                    {
                        if (a.CanWrite)
                            a.SetValue(c, a.GetValue(c2));
                        else
                            Debug.WriteLine("CommentXmlConfigReaderNotifyWhenChanged had an issue setting c." + a.Name);
                        c?.PropertyChanged(this, new PropertyChangedEventArgsFromFile(a.Name));
                    }
            }
            catch (Exception e)
            {
                //dont kill entire app for that
                Debug.WriteLine(e);
            }
        };
        fileSystemWatchers.Add(j);
        return c;
    }
}
public class PropertyChangedEventArgsFromFile(string? propertyName) : PropertyChangedEventArgs(propertyName)
{
}