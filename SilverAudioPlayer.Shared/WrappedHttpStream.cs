using SilverMagicBytes;

namespace SilverAudioPlayer.Shared
{
    public class WrappedHttpStream : WrappedStream, IDisposable
    {
        private bool disposedValue;

        public WrappedHttpStream(string url)
        {
            URL = url;
        }

        public string URL { get; set; }
        public List<Stream> Streams { get; set; } = new();
        public override MimeType MimeType { get => _MimeType; }
        private MimeType _MimeType { get; set; }

        private Stream InternalGetStream()
        {
            var content = HttpClient.Client.GetAsync(URL).GetAwaiter().GetResult();
            var Stream = content.Content.ReadAsStream();
            Streams.Add(Stream);
            return Stream;
        }

        public override Stream GetStream()
        {
            var content = HttpClient.Client.GetAsync(URL).GetAwaiter().GetResult();
            var Stream = content.Content.ReadAsStream();
            Streams.Add(Stream);
            MimeType? mt = KnownMimes.GetKnownMimeByName(content.Content.Headers.ContentType?.MediaType);
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