using SilverMagicBytes;
using System.Reflection;

namespace SilverAudioPlayer.Shared
{
    public class WrappedEmbeddedResourceStream : WrappedStream, IDisposable
    {
        public WrappedEmbeddedResourceStream(Assembly assembly, string resourceLoc)
        {
            Assembly = assembly;
            URL = resourceLoc;
        }
        private bool disposedValue;
        Assembly Assembly;
        public string URL { get; set; }
        public List<Stream> Streams { get; set; } = new();
        public override MimeType MimeType { get => _MimeType; }
        private MimeType _MimeType { get; set; }
        private Stream InternalGetStream()
        {
            var Stream = Assembly.GetManifestResourceStream(URL);
            Streams.Add(Stream);
            return Stream;
        }

        public override Stream GetStream()
        {
            var Stream = InternalGetStream();
            MimeType? mt;
            using (var stream2 = InternalGetStream())
            {
                mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
                Streams.Remove(stream2);
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
