using SilverMagicBytes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        public override string MimeType { get => _MimeType; }
        private string _MimeType { get; set; } = "application/octet-stream";
        private Stream InternalGetStream()
        {
            var Stream = Assembly.GetManifestResourceStream(URL);
            Streams.Add(Stream);
            return Stream;
        }

        public override Stream GetStream()
        {
            var Stream = InternalGetStream();
            string mt;
            using (var stream2 = InternalGetStream())
            {
                mt = MagicByteCombos.Match(stream2, 0)?.MimeType;
                Streams.Remove(stream2);
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
