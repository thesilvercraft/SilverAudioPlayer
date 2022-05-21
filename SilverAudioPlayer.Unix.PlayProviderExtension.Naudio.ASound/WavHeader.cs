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
    /// Wave header
    /// </summary>
    public struct WavHeader
    {
        /// <summary>
        /// The wave chunk header
        /// </summary>
        public WavHeaderChunk Chunk { get; set; }

        /// <summary>
        /// The format
        /// </summary>
        public char[] Format { get; set; }

        /// <summary>
        /// First sub chunk
        /// </summary>
        public WavHeaderChunk SubChunk1 { get; set; }

        /// <summary>
        /// Audio format
        /// </summary>
        public ushort AudioFormat { get; set; }

        /// <summary>
        /// Number of channels
        /// </summary>
        public ushort NumChannels { get; set; }

        /// <summary>
        /// Sample rate
        /// </summary>
        public uint SampleRate { get; set; }

        /// <summary>
        /// Byte rate
        /// </summary>
        public uint ByteRate { get; set; }

        /// <summary>
        /// Block alignment
        /// </summary>
        public ushort BlockAlign { get; set; }

        /// <summary>
        /// Bits per sample
        /// </summary>
        public ushort BitsPerSample { get; set; }

        /// <summary>
        /// Second sub chunk
        /// </summary>
        public WavHeaderChunk SubChunk2 { get; set; }
    }
}