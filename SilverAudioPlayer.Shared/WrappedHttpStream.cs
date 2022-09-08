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
        public override string MimeType { get => _MimeType; }
        private string _MimeType { get; set; } = "application/octet-stream";

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
            var mt = content.Content.Headers.ContentType?.MediaType;
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
            mt ??= "application/octet-stream";
            _MimeType = mt.RealMimeTypeToFakeMimeType();
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