﻿using SilverMagicBytes;

namespace SilverAudioPlayer.Shared;

public interface IPlayProvider : ICodeInformation
{
    public IReadOnlyList<MimeType>? SupportedMimes { get; }
    public IPlayProviderListner ProviderListener { set; }

    /// <summary>
    ///     Determines if the following provider can play a certain file
    ///     Notice: this method should not throw any kind of exceptions
    /// </summary>
    /// <param name="stream">the stream</param>
    /// <returns>a true or false based on if the file can be played with a player this provider provides</returns>
    bool CanPlayFile(WrappedStream stream);

    /// <summary>
    ///     Gets the most suited player that is able to play this file
    ///     Notice: this method should not throw any kind of exceptions (just return null) but throwing exceptions is fine here
    /// </summary>
    /// <param name="URI">the file</param>
    /// <returns>a player or null if the provider is unable to find a player suited for the job</returns>
    IPlay? GetPlayer(WrappedStream stream);

    Task OnStartup();
}

public interface IWakeLockProvider : ICodeInformation
{
    public void WakeLock();
    public void UnWakeLock();
}

public interface IPlayProviderListner
{
    IPlayerEnviroment GetEnviroment();
}