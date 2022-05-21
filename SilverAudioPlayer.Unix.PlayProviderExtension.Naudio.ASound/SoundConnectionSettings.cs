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
    /// The connection settings of a sound device.
    /// </summary>
    public class SoundConnectionSettings
    {
        /// <summary>
        /// The playback device name of the sound device is connected to.
        /// </summary>
        public string PlaybackDeviceName { get; set; } = "default";

        /// <summary>
        /// The recording device name of the sound device is connected to.
        /// </summary>
        public string RecordingDeviceName { get; set; } = "default";

        /// <summary>
        /// The mixer device name of the sound device is connected to.
        /// </summary>
        public string MixerDeviceName { get; set; } = "default";

        /// <summary>
        /// The sample rate of recording.
        /// </summary>
        public uint RecordingSampleRate { get; set; } = 8000;

        /// <summary>
        /// The channels of recording.
        /// </summary>
        public ushort RecordingChannels { get; set; } = 2;

        /// <summary>
        /// The bits per sample of recording.
        /// </summary>
        public ushort RecordingBitsPerSample { get; set; } = 16;
    }
}