using SilverMagicBytes;

namespace SilverAudioPlayer.Shared
{
    public class WrappedFileStream : WrappedStream, IDisposable
    {
        private bool disposedValue;

        public WrappedFileStream(string url)
        {
            URL = url;
        }

        public string URL { get; set; }
        public List<Stream> Streams { get; set; } = new();

        public override MimeType MimeType { get => _MimeType; }
        private MimeType _MimeType { get; set; } 

        private Stream InternalGetStream()
        {
            var Stream = new FileStream(URL, FileMode.Open, FileAccess.Read, FileShare.Read);
            Streams.Add(Stream);
            return Stream;
        }

        public override Stream GetStream()
        {
            var Stream = InternalGetStream();
            MimeType? mt = null;
            if (mt == null)
            {
                var stream2 = InternalGetStream();
                try
                {
                    mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
                }
                finally
                {
                    stream2.Dispose();
                    Streams.Remove(stream2);
                }
            }
            if (mt == null)
            {
                mt = KnownMimes.GetKnownMimeByExtension(new FileInfo(URL).Extension);
            }
            _MimeType = mt;
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