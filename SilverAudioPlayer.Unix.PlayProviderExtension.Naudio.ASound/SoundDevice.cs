// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
/*
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace Iot.Device.Media
{
    /// <summary>
    /// The communications channel to a sound device.
    /// </summary>
    public abstract partial class SoundDevice : IDisposable
    {
        /// <summary>
        /// Create a communications channel to a sound device running on Unix.
        /// </summary>
        /// <param name="settings">The connection settings of a sound device.</param>
        /// <returns>A communications channel to a sound device running on Unix.</returns>
        public static SoundDevice Create(SoundConnectionSettings settings) => new UnixSoundDevice(settings);

        /// <summary>
        /// The connection settings of the sound device.
        /// </summary>
        public abstract SoundConnectionSettings Settings { get; }

        /// <summary>
        /// The playback volume of the sound device.
        /// </summary>
        public abstract long PlaybackVolume { get; set; }

        /// <summary>
        /// The playback mute of the sound device.
        /// </summary>
        public abstract bool PlaybackMute { get; set; }

        /// <summary>
        /// The recording volume of the sound device.
        /// </summary>
        public abstract long RecordingVolume { get; set; }

        /// <summary>
        /// The recording mute of the sound device.
        /// </summary>
        public abstract bool RecordingMute { get; set; }

        /// <summary>
        /// Play WAV file.
        /// </summary>
        /// <param name="wavPath">WAV file path.</param>
        public abstract void Play(string wavPath);

        /// <summary>
        /// Play WAV file.
        /// </summary>
        /// <param name="wavStream">WAV stream.</param>
        public abstract void Play(Stream wavStream);

        /// <summary>
        /// Sound recording.
        /// </summary>
        /// <param name="recordTimeSeconds">Recording duration(In seconds).</param>
        /// <param name="outputFilePath">Recording save path.</param>
        public abstract void Record(uint recordTimeSeconds, string outputFilePath);

        /// <summary>
        /// Sound recording.
        /// </summary>
        /// <param name="recordTimeSeconds">Recording duration(In seconds).</param>
        /// <param name="outputStream">Recording save stream.</param>
        public abstract void Record(uint recordTimeSeconds, Stream outputStream);

        /// <summary>
        /// Start a continuous recording
        /// </summary>
        /// <param name="outputFilePath">The path of the output file</param>
        public abstract void StartRecording(string outputFilePath);

        /// <summary>
        /// Stop the continuous recording
        /// </summary>
        public abstract void StopRecording();

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Write a header in a Stream
        /// </summary>
        /// <param name="wavStream">A wave stream</param>
        /// <param name="header">The header to add</param>
        public abstract void WriteWavHeader(Stream wavStream, WavHeader header);

        /// <summary>
        /// Releases the unmanaged resources used by the SoundDevice and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}