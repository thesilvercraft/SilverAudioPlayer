﻿#define DIAGNOSTICS

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NAudio.Flac.Metadata;
using NAudio.Wave;

namespace NAudio.Flac
{
    /// <summary>
    ///     Provides a decoder for decoding flac (Free Lossless Audio Codec) data.
    /// </summary>
    public class FlacReader : WaveStream, IDisposable
    {
        private readonly object _bufferLock = new object();

        private readonly bool _closeStream;
        private readonly FlacPreScan _scan;
        private readonly Stream _stream;
        public readonly FlacMetadataStreamInfo _streamInfo;
        private readonly WaveFormat _waveFormat;

        private FlacFrame _frame;

        private int _frameIndex;

        //overflow:
        private byte[] _overflowBuffer;

        private int _overflowCount;
        private int _overflowOffset;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="fileName">Filename which of a flac file which should be decoded.</param>
        public FlacReader(string fileName)
            : this(File.OpenRead(fileName))
        {
            _closeStream = true;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        public FlacReader(Stream stream)
            : this(stream, FlacPreScanMode.Default)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        /// <param name="scanFlag">Scan mode which defines how to scan the flac data for frames.</param>
        public FlacReader(Stream stream, FlacPreScanMode scanFlag)
            : this(stream, scanFlag, null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlacReader" /> class.
        /// </summary>
        /// <param name="stream">Stream which contains flac data which should be decoded.</param>
        /// <param name="scanFlag">Scan mode which defines how to scan the flac data for frames.</param>
        /// <param name="onscanFinished">
        ///     Callback which gets called when the pre scan processes finished. Should be used if the
        ///     <paramref name="scanFlag" /> argument is set the <see cref="FlacPreScanMode.Async" />.
        /// </param>
        public FlacReader(Stream stream, FlacPreScanMode scanFlag,
            Action<FlacPreScanFinishedEventArgs> onscanFinished)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead) throw new ArgumentException("Stream is not readable.", nameof(stream));

            _stream = stream;
            _closeStream = true;
            //read fLaC sync
            var beginSync = new byte[4];
            var read = stream.Read(beginSync, 0, beginSync.Length);
            if (read < beginSync.Length) throw new EndOfStreamException("Can not read \"fLaC\" sync.");

            if (beginSync[0] == 0x66 && beginSync[1] == 0x4C && //Check for 'fLaC' signature
                beginSync[2] == 0x61 && beginSync[3] == 0x43)
            {
                //read metadata
                var metadata = FlacMetadata.ReadAllMetadataFromStream(stream).ToList();

                Metadata = metadata.AsReadOnly();
                if (metadata.Count <= 0) throw new FlacException("No Metadata found.", FlacLayer.Metadata);

                if (!(metadata.First(x => x.MetaDataType == FlacMetaDataType.StreamInfo) is FlacMetadataStreamInfo
                        streamInfo)) throw new FlacException("No StreamInfo-Metadata found.", FlacLayer.Metadata);

                _streamInfo = streamInfo;
                _waveFormat = CreateWaveFormat(streamInfo);
                Debug.WriteLine("Flac StreamInfo found -> WaveFormat: " + _waveFormat);
                Debug.WriteLine("Flac-File-Metadata read.");
            }
            else
            {
                throw new FlacException("Invalid Flac-File. \"fLaC\" Sync not found.", FlacLayer.OutSideOfFrame);
            }

            //prescan stream
            if (scanFlag != FlacPreScanMode.None)
            {
                var scan = new FlacPreScan(stream);
                scan.ScanFinished += (s, e) => onscanFinished?.Invoke(e);
                scan.ScanStream(_streamInfo, scanFlag);
                _scan = scan;
            }
        }

        /// <summary>
        ///     Gets a list with all found metadata fields.
        /// </summary>
        public ReadOnlyCollection<FlacMetadata> Metadata { get; protected set; }

        /// <summary>
        ///     Gets the output <see cref="CSCore.WaveFormat" /> of the decoder.
        /// </summary>
        public override WaveFormat WaveFormat => _waveFormat;

        /// <summary>
        ///     Gets a value which indicates whether the seeking is supported. True means that seeking is supported; False means
        ///     that seeking is not supported.
        /// </summary>
        public override bool CanSeek => _scan != null;

        private FlacFrame Frame => _frame ?? (_frame = FlacFrame.FromStream(_stream, _streamInfo));

        /// <summary>
        ///     Gets or sets the position of the <see cref="FlacReader" /> in bytes.
        /// </summary>
        public override long Position
        {
            get
            {
                if (!CanSeek || _disposed) return 0;

                lock (_bufferLock)
                {
#if !DIAGNOSTICS
                    if (_frameIndex == _scan.Frames.Count)
                        return Length;
                    return _scan.Frames[_frameIndex].SampleOffset * WaveFormat.BlockAlign + _overflowOffset;
#else
                    return _position;
#endif
                }
            }
            set
            {
                CheckForDisposed();

                if (!CanSeek) return;

                lock (_bufferLock)
                {
                    value = Math.Max(Math.Min(value, Length), 0);
                    value -= value % WaveFormat.BlockAlign;

                    for (var i = 0; i < _scan.Frames.Count; i++)
                        if (value / WaveFormat.BlockAlign <= _scan.Frames[i].SampleOffset)
                        {
                            if (i != 0) i--;

                            _stream.Position = _scan.Frames[i].StreamOffset;
                            _frameIndex = i;
                            if (_stream.Position >= _stream.Length) throw new EndOfStreamException("Stream got EOF.");
#if DIAGNOSTICS
                            _position = _scan.Frames[i].SampleOffset * WaveFormat.BlockAlign;
#endif
                            _overflowCount = 0;
                            _overflowOffset = 0;

                            var diff = (int)(value - Position);
                            diff -= diff % WaveFormat.BlockAlign;
                            if (diff > 0) ReadBytes(diff);

                            break;
                        }
                }
            }
        }

        /// <summary>
        ///     Gets the length of the <see cref="FlacReader" /> in bytes.
        /// </summary>
        public override long Length
        {
            get
            {
                if (_disposed) return 0;

                if (CanSeek) return _scan.TotalSamples * WaveFormat.BlockAlign;

                return -1;
            }
        }

        /// <summary>
        ///     Disposes the <see cref="FlacReader" /> instance and disposes the underlying stream.
        /// </summary>
        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private WaveFormat CreateWaveFormat(FlacMetadataStreamInfo streamInfo)
        {
            return new WaveFormat(streamInfo.SampleRate, streamInfo.BitsPerSample, streamInfo.Channels);
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="FlacReader" /> and advances the position within the stream by the
        ///     number of bytes read.
        /// </summary>
        /// <param name="buffer">
        ///     An array of bytes. When this method returns, the <paramref name="buffer" /> contains the specified
        ///     byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> +
        ///     <paramref name="count" /> - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        ///     The zero-based byte offset in the <paramref name="buffer" /> at which to begin storing the data
        ///     read from the current stream.
        /// </param>
        /// <param name="count">The maximum number of bytes to read from the current source.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckForDisposed();

            var read = 0;
            count -= count % WaveFormat.BlockAlign;

            lock (_bufferLock)
            {
                read += GetOverflows(buffer, ref offset, count);

                while (read < count)
                {
                    var frame = Frame;
                    if (frame == null) return read;

                    while (!frame.NextFrame())
                        if (CanSeek) //go to next frame
                        {
                            if (++_frameIndex >= _scan.Frames.Count) return read;

                            _stream.Position = _scan.Frames[_frameIndex].StreamOffset;
                        }

                    _frameIndex++;

                    var bufferlength = frame.GetBuffer(ref _overflowBuffer);
                    var bytesToCopy = Math.Min(count - read, bufferlength);
                    Array.Copy(_overflowBuffer, 0, buffer, offset, bytesToCopy);
                    read += bytesToCopy;
                    offset += bytesToCopy;

                    _overflowCount = bufferlength > bytesToCopy ? bufferlength - bytesToCopy : 0;
                    _overflowOffset = bufferlength > bytesToCopy ? bytesToCopy : 0;
                }
            }
#if DIAGNOSTICS
            _position += read;
#endif

            return read;
        }

        private int GetOverflows(byte[] buffer, ref int offset, int count)
        {
            if (_overflowCount != 0 && _overflowBuffer != null && count > 0)
            {
                var bytesToCopy = Math.Min(count, _overflowCount);
                Array.Copy(_overflowBuffer, _overflowOffset, buffer, offset, bytesToCopy);

                _overflowCount -= bytesToCopy;
                _overflowOffset += bytesToCopy;
                offset += bytesToCopy;
                return bytesToCopy;
            }

            return 0;
        }

        /// <summary>
        ///     Disposes the <see cref="FlacReader" /> instance and disposes the underlying stream.
        /// </summary>
        /// <param name="disposing">
        ///     True to release both managed and unmanaged resources; false to release only unmanaged
        ///     resources.
        /// </param>
        protected new virtual void Dispose(bool disposing)
        {
            lock (_bufferLock)
            {
                if (!_disposed)
                {
                    if (_frame != null)
                    {
                        _frame.Dispose();
                        _frame = null;
                    }

                    if (_stream != null && _closeStream) _stream.Dispose();

                    _disposed = true;
                }
            }
        }

        internal byte[] ReadBytes(int count)
        {
            count -= count % WaveFormat.BlockAlign;
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            var buffer = new byte[count];
            var read = Read(buffer, 0, buffer.Length);
            if (read < count) Array.Resize(ref buffer, read);

            return buffer;
        }

        private void CheckForDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        ///     Destructor which calls the <see cref="Dispose(bool)" /> method.
        /// </summary>
        ~FlacReader()
        {
            Dispose(false);
        }

#if DIAGNOSTICS
        private long _position;
        private bool _disposed;
#endif
    }
}