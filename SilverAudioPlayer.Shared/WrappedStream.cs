﻿using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public abstract class WrappedStream : IDisposable
{
    public abstract MimeType MimeType { get; }

    public abstract Stream GetStream();
    /// <summary>
    /// Should the code that got a stream from a GetStream call dispose it after it is no longer needed
    /// </summary>
    public abstract bool ShouldDisposeStream { get; }
    public abstract void Dispose();
}

public abstract class WrappedRegenerativeStream:WrappedStream
{
    /// <summary>
    /// A new stream sharing the same data as the previous one (if any)
    /// </summary>
    /// <returns>A read only stream</returns>
    public abstract override  Stream GetStream();
}