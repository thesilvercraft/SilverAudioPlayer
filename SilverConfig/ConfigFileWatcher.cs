using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverConfig
{
    public class ConfigFileWatcher : IDisposable
    {
        private bool disposedValue;

        public ConfigFileWatcher(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' cannot be null or whitespace.", nameof(filePath));
            }
            if (!File.Exists(filePath))
            {
                throw new ArgumentException($"'{nameof(filePath)}' does not exist.", nameof(filePath));
            }
            FilePath = filePath;
            Watcher = new(new FileInfo(FilePath)!.Directory!.FullName, "*.xml");
            Watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            Watcher.Changed += OnChanged;
            Watcher.Created += OnCreated;
            Watcher.Deleted += OnDeleted;
            Watcher.Renamed += OnRenamed;
            Watcher.Error += OnError;
            Watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            Debug.WriteLine($"Changed: {e.FullPath}");
            if (e.FullPath == FilePath)
            {
                OnChangedE.Invoke(this, e.FullPath);
            }
        }

        public event EventHandler<string> OnChangedE;

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            string value = $"Created: {e.FullPath}";
            Debug.WriteLine(value);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e) =>
            Debug.WriteLine($"Deleted: {e.FullPath}");

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            Debug.WriteLine($"Renamed:");
            Debug.WriteLine($"    Old: {e.OldFullPath}");
            Debug.WriteLine($"    New: {e.FullPath}");
        }

        private void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Debug.WriteLine($"Message: {ex.Message}");
                Debug.WriteLine("Stacktrace:");
                Debug.WriteLine(ex.StackTrace);
                PrintException(ex.InnerException);
            }
        }

        private FileSystemWatcher Watcher { get; set; }
        public string FilePath { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Watcher.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}