using System.Diagnostics;
using System.Net;
using NAudio.Wave;
using NLayer.NAudioSupport;
using SilverAudioPlayer.NAudio;
using SilverAudioPlayer.Shared;
using SilverMagicBytes;
using PlaybackState = SilverAudioPlayer.Shared.PlaybackState;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public class Mp3StreamPlayer :IPlayStreams
{
    private WaveFilePlayer _player;
    public Mp3StreamPlayer(ref WaveFilePlayer player, Stream stream)
    {
        this.stream = stream;
        _player=player;
        if (player.outputDevice == null)
        {
            player.InitBackEnd();
        }

        t = new(timer1_Tick, null, 0, 250);
        
        ThreadPool.QueueUserWorkItem(StreamMp3);
    }

    private Timer t;
    private void timer1_Tick(object? sender)
    {
        if (State != PlaybackState.Stopped)
        {
            if (Startplaying && bufferedWaveProvider != null)
            {
                _player.outputDevice.Init(bufferedWaveProvider);
                    _player.outputDevice.Play();
                    Startplaying = false;
            //progressBarBuffer.Maximum = (int)bufferedWaveProvider.BufferDuration.TotalMilliseconds;
            }
            else if (bufferedWaveProvider != null)
            {
                var bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;
                //ShowBufferState(bufferedSeconds);
                // make it stutter less if we buffer up a decent amount before playing
                if (bufferedSeconds < 0.5 && State == PlaybackState.Playing && !fullyDownloaded)
                {
                    Pause();
                }
                else if (bufferedSeconds > 4 && State == PlaybackState.Buffering)
                {
                    Play();
                }
                else if (fullyDownloaded && bufferedSeconds == 0)
                {
                    Debug.WriteLine("Reached end of stream");
                    State = PlaybackState.Stopped;
                    Stop();
                }
            }

        }
    }

    public void Dispose()
    {
    }

    private bool Startplaying = false;
    public void Play()
    {Startplaying = true;
        if (_player.outputDevice.PlaybackState == global::NAudio.Wave.PlaybackState.Stopped) return;

    _player.outputDevice.Play();
        State = PlaybackState.Playing;
    }

    public void Stop()
    {
        _player.outputDevice.Stop();
        t.Dispose();
    }

    public void Pause()
    {
        State = PlaybackState.Buffering;
        _player.outputDevice.Pause();
    }

    public void Resume()
    {
        _player.outputDevice.Play();
        State = PlaybackState.Playing;
    }

    public uint? ChannelCount()
    {
        return null;
    }

    public void SetVolume(byte volume)
    {
    }

    public TimeSpan GetPosition()
    {
        return TimeSpan.Zero;
    }

    public void SetPosition(TimeSpan position)
    {
        //throw new NotImplementedException();
    }

    public PlaybackState? GetPlaybackState()
    {
        return State;
    }

    public TimeSpan? Length()
    {
        return null;
    }

    public long? GetSampleRate()
    {
        return null;
    }

    public uint? GetBitsPerSample()
    {
        return null;
    }

    public event EventHandler<object>? TrackEnd;
    public event EventHandler<object>? TrackPause;
    public event EventHandler<TimeSpan>? PositionChanged;
    public bool IsTrackLoaded { get; private set; }
    public void LoadStream(WrappedStream stream)
    {
       this.stream = stream.GetStream();
       IsTrackLoaded = true;
    }
    private BufferedWaveProvider bufferedWaveProvider;
    private bool IsBufferNearlyFull
    {
        get
        {
            return bufferedWaveProvider != null && 
                   bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes 
                   < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
        }
    }
    private bool fullyDownloaded = false;
    private Stream stream;
    private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
    {
        WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
            frame.FrameLength, frame.BitRate);
        return new Mp3FrameDecompressor(waveFormat);
    }

    private PlaybackState State = PlaybackState.Playing;
     private void StreamMp3(object? state)
        {
            fullyDownloaded = false;
           
            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (stream)
                {
                    var readFullyStream = new ReadFullyStream(stream);
                    do
                    {
                        if (IsBufferNearlyFull)
                        {
                            Debug.WriteLine("Buffer getting full, taking a break");
                            Thread.Sleep(500);
                        }
                        else
                        {
                            Mp3Frame frame;
                            try
                            {
                                frame = Mp3Frame.LoadFromStream(readFullyStream);
                            }
                            catch (EndOfStreamException)
                            {
                                fullyDownloaded = true;
                                // reached the end of the MP3 file / stream
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread
                                break;
                            }
                            if (frame == null) break;
                            if (decompressor == null)
                            {
                                // don't think these details matter too much - just help ACM select the right codec
                                // however, the buffered provider doesn't know what sample rate it is working at
                                // until we have a frame
                                decompressor = CreateFrameDecompressor(frame);
                                bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat);
                                bufferedWaveProvider.BufferDuration =
                                    TimeSpan.FromSeconds(20); // allow us to get well ahead of ourselves
                                //this.bufferedWaveProvider.BufferedDuration = 250;
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                            bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                        }

                    } while (State != PlaybackState.Stopped);
                    Debug.WriteLine("Exiting");
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                if (decompressor != null)
                {
                    decompressor.Dispose();
                }
            }
        }
     // Eh it does support mp3 mimes but nah since its a stream i handle it differently
    public IReadOnlyList<MimeType>? SupportedMimes { get; } = null; 
}
 public class ReadFullyStream : Stream
    {
        private readonly Stream sourceStream;
        private long pos; // psuedo-position
        private readonly byte[] readAheadBuffer;
        private int readAheadLength;
        private int readAheadOffset;

        public ReadFullyStream(Stream sourceStream)
        {
            this.sourceStream = sourceStream;
            readAheadBuffer = new byte[4096];
        }
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override long Length
        {
            get { return pos; }
        }

        public override long Position
        {
            get
            {
                return pos;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                int readAheadAvailableBytes = readAheadLength - readAheadOffset;
                int bytesRequired = count - bytesRead;
                if (readAheadAvailableBytes > 0)
                {
                    int toCopy = Math.Min(readAheadAvailableBytes, bytesRequired);
                    Array.Copy(readAheadBuffer, readAheadOffset, buffer, offset + bytesRead, toCopy);
                    bytesRead += toCopy;
                    readAheadOffset += toCopy;
                }
                else
                {
                    readAheadOffset = 0;
                    readAheadLength = sourceStream.Read(readAheadBuffer, 0, readAheadBuffer.Length);
                    //Debug.WriteLine(String.Format("Read {0} bytes (requested {1})", readAheadLength, readAheadBuffer.Length));
                    if (readAheadLength == 0)
                    {
                        break;
                    }
                }
            }
            pos += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }