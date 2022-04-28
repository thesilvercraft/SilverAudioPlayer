namespace SilverAudioPlayer.Shared
{
    public class WrappedFileStream : WrappedStream, IDisposable
    {
        private bool disposedValue;

        public WrappedFileStream(string url)
        {
            URL = url;
            Stream = File.Open(URL, FileMode.Open);
        }

        public string URL { get; set; }

        public override Stream RegenStream()
        {
            if (Stream != null && Stream.CanSeek && Stream.CanRead)
            {
                Stream.Close();
            }

            Stream = File.Open(URL, FileMode.Open);
            return Stream;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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