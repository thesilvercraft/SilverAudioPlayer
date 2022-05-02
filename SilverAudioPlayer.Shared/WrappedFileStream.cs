using System.Diagnostics;

namespace SilverAudioPlayer.Shared
{
    public class WrappedFileStream : WrappedStream, IDisposable
    {
        private bool disposedValue;

        public WrappedFileStream(string url)
        {
            URL = url;
            RegenStream();
        }

        public string URL { get; set; }
        public List<Stream> Streams { get; set; } = new();

        public override Stream RegenStream()
        {
            Stream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.Read);
            Streams.Add(Stream);
            return Stream;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var stream in Streams.Where(x => x.CanRead))
                    {
                        stream.Dispose();
                    }
                    // Stream.Dispose();
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