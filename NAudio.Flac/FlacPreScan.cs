﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Flac.Metadata;

namespace NAudio.Flac
{
    public sealed class FlacPreScan
    {
        private const int BufferSize = 50000;
        private readonly Stream _stream;
        private bool _isRunning;

        public FlacPreScan(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead) throw new ArgumentException("stream is not readable");

            _stream = stream;
        }

        public List<FlacFrameInformation> Frames { get; private set; }

        public long TotalLength { get; private set; }

        public long TotalSamples { get; private set; }

        public event EventHandler<FlacPreScanFinishedEventArgs> ScanFinished;

        public void ScanStream(FlacMetadataStreamInfo streamInfo, FlacPreScanMode mode)
        {
            var saveOffset = _stream.Position;
            StartScan(streamInfo, mode);
            _stream.Position = saveOffset;

            long totalLength = 0, totalsamples = 0;
            foreach (var frame in Frames.Select(Frame => Frame.Header))
            {
                totalLength += frame.BlockSize * frame.BitsPerSample * frame.Channels;
                totalsamples += frame.BlockSize;
            }

            TotalLength = totalLength;
            TotalSamples = totalsamples;

            Debug.WriteLineIf(TotalSamples == streamInfo.TotalSamples,
                "Flac prescan successful. Calculated total_samples value matching the streaminfo-metadata.");
        }

        private void StartScan(FlacMetadataStreamInfo streamInfo, FlacPreScanMode mode)
        {
            if (_isRunning) throw new Exception("Scan is already running.");

            _isRunning = true;

            if (mode == FlacPreScanMode.Async)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Frames = RunScan(streamInfo);
                    _isRunning = false;
                });
            }
            else
            {
                Frames = RunScan(streamInfo);
                _isRunning = false;
            }
        }

        private List<FlacFrameInformation> RunScan(FlacMetadataStreamInfo streamInfo)
        {
#if FLAC_DEBUG
            Stopwatch watch = new Stopwatch();
            watch.Start();
#endif
            var result = ScanThisShit(streamInfo);

#if FLAC_DEBUG
            watch.Stop();
            Debug.WriteLine(String.Format("FlacPreScan finished: {0} Bytes processed in {1} ms.",
                _stream.Length, watch.ElapsedMilliseconds));
#endif
            RaiseScanFinished(result);
            return result;
        }

        private void RaiseScanFinished(List<FlacFrameInformation> frames)
        {
            ScanFinished?.Invoke(this, new FlacPreScanFinishedEventArgs(frames));
        }

        private unsafe List<FlacFrameInformation> ScanThisShit(FlacMetadataStreamInfo streamInfo)
        {
            var stream = _stream;
            var buffer = new byte[BufferSize];
            stream.Position = 4; //fLaC

            //skip the metadata
            FlacMetadata.SkipMetadata(stream);

            var frames = new List<FlacFrameInformation>();
            var frameInfo = new FlacFrameInformation
            {
                IsFirstFrame = true
            };

            FlacFrameHeader baseHeader = null;

            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= FlacConstant.FrameHeaderSize) break;

                fixed (byte* bufferPtr = buffer)
                {
                    var ptr = bufferPtr;
                    while (bufferPtr + read - FlacConstant.FrameHeaderSize > ptr)
                        if (*ptr++ == 0xFF && (*ptr & 0xF8) == 0xF8) //check sync
                        {
                            var ptrSafe = ptr;
                            ptr--;
                            if (IsFrame(ref ptr, streamInfo, out var tmp))
                            {
                                var header = tmp;
                                if (frameInfo.IsFirstFrame)
                                {
                                    baseHeader = header;
                                    frameInfo.IsFirstFrame = false;
                                }

                                if (baseHeader?.IsFormatEqualTo(header) == true)
                                {
                                    frameInfo.StreamOffset = stream.Position - read + (ptrSafe - 1 - bufferPtr);
                                    frameInfo.Header = header;
                                    frames.Add(frameInfo);

                                    frameInfo.SampleOffset += header.BlockSize;
                                }
                                else
                                {
                                    ptr = ptrSafe;
                                }
                            }
                            else
                            {
                                ptr = ptrSafe;
                            }
                        }
                }

                stream.Position -= FlacConstant.FrameHeaderSize;
            }

            return frames;
        }

        private unsafe bool IsFrame(ref byte* buffer, FlacMetadataStreamInfo streamInfo, out FlacFrameHeader header)
        {
            header = new FlacFrameHeader(ref buffer, streamInfo, true, false);
            return !header.HasError;
        }
    }
}