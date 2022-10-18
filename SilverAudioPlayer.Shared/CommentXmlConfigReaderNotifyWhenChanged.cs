using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using SilverConfig;
namespace SilverConfig.CobaltExtensions
{
    public interface ILetNotify
    {
        void Invoke(object e, PropertyChangedEventArgs a);
        bool AllowedToRead { get; }
    }

    public class CommentXmlConfigReaderNotifyWhenChanged<T> : CommentXmlConfigReader<T>, IDisposable where T : INotifyPropertyChanged, ILetNotify
    {
        private readonly List<FileSystemWatcher> fileSystemWatchers = new();

        public override T? Read(string path)
        {
            var c = base.Read(path);
            var fp = Path.GetFullPath(path);
            var fpdir = Path.GetDirectoryName(fp);
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
                if (y.ChangeType != WatcherChangeTypes.Changed)
                {
                    return;
                }
                if (y.FullPath == fp && c.AllowedToRead)
                {
                    var c2 = base.Read(path);
                    var t = typeof(T);
                    foreach (var a in t.GetProperties())
                    {
                        if (a.Name!= "AllowedToRead" && a.CanRead && (a.GetValue(c)?.Equals(a.GetValue(c2)) != true))
                        {
                            if (a.CanWrite)
                            {
                                a.SetValue(c, a.GetValue(c2));
                            }
                            else
                            {
                                Debug.WriteLine("CommentXmlConfigReaderNotifyWhenChanged had an issue setting c." + a.Name);
                            }
                            c?.Invoke(this, new(a.Name));
                        }
                    }
                }
            };
            fileSystemWatchers.Add(j);
            return c;
        }

        public void Dispose()
        {
            foreach (var fsw in fileSystemWatchers)
            {
                fsw.Dispose();
            }
        }
    }
}