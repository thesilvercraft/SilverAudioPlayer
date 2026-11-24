using System.Security.Cryptography;

namespace SilverAudioPlayer.Shared;

/// <summary>
/// A stream pool, a hashtable containing streams and reliance on those streams
/// </summary>
public interface ISharedMemoryStreamPool
{
    public SharedStream GetFromByteArray(byte[] bytes, bool copy = true);
    public SharedStream GetFromWrappedStream(WrappedStream ws);
    public (SharedStream Stream, RelianceOnSharedStream Reliance) AcquireFromByteArray(byte[] bytes, bool copy = true)
    {
        var shared = GetFromByteArray(bytes, copy);
        return (shared, new RelianceOnSharedStream(shared, this));
    }
    public (SharedStream Stream, RelianceOnSharedStream Reliance) AcquireFromWrappedStream(WrappedStream ws)
    {
        var shared = GetFromWrappedStream(ws);
        return (shared, new RelianceOnSharedStream(shared, this));
    }
    Dictionary<Guid, List<RelianceOnSharedStream>> Reliances { get; }
    public void AddReliance(RelianceOnSharedStream reliance);
    public void RemoveReliance(RelianceOnSharedStream reliance);
}

public class SharedMemoryStreamPool : ISharedMemoryStreamPool
{
    private readonly Dictionary<Guid, List<RelianceOnSharedStream>> _reliances = new();
    private readonly Dictionary<string, SharedStream> _streamsByHash = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, SharedStream> _streamsById = new();
    private readonly Lock _reliancesLock = new();

    public SharedStream GetFromByteArray(byte[] bytes, bool copy = true)
    {
        var hash = HashHelper.HashByteArray(bytes);
        lock (_reliancesLock)
        {
            if (_streamsByHash.TryGetValue(hash, out var sharedStream))
                return sharedStream;

            var memoryStream = new WrappedSharedMemoryStream(bytes, copy: copy);
            var sharedStream2 = new SharedStream(memoryStream, hash);
            AddStreamLocked(sharedStream2);
            return sharedStream2;
        }
    }

    public SharedStream GetFromWrappedStream(WrappedStream ws) //TODO: group by path if wrappedfilestream
    {
        var hash = HashHelper.Hash(ws);
        lock (_reliancesLock)
        {
            if (_streamsByHash.TryGetValue(hash, out var sharedStream))
            {
                ws.Dispose();
                return sharedStream;
            }

            var sharedStream2 = new SharedStream(ws, hash);
            AddStreamLocked(sharedStream2);
            return sharedStream2;
        }
    }

    public (SharedStream Stream, RelianceOnSharedStream Reliance) AcquireFromByteArray(byte[] bytes, bool copy = true)
    {
        var hash = HashHelper.HashByteArray(bytes);
        lock (_reliancesLock)
        {
            if (!_streamsByHash.TryGetValue(hash, out var sharedStream))
            {
                var memoryStream = new WrappedSharedMemoryStream(bytes, copy: copy);
                sharedStream = new SharedStream(memoryStream, hash);
                AddStreamLocked(sharedStream);
            }

            var reliance = new RelianceOnSharedStream(sharedStream, this);
            return (sharedStream, reliance);
        }
    }

    public (SharedStream Stream, RelianceOnSharedStream Reliance) AcquireFromWrappedStream(WrappedStream ws)
    {
        var hash = HashHelper.Hash(ws);
        lock (_reliancesLock)
        {
            if (_streamsByHash.TryGetValue(hash, out var sharedStream))
            {
                ws.Dispose();
            }
            else
            {
                sharedStream = new SharedStream(ws, hash);
                AddStreamLocked(sharedStream);
            }

            var reliance = new RelianceOnSharedStream(sharedStream, this);
            return (sharedStream, reliance);
        }
    }

    private void AddStreamLocked(SharedStream sharedStream)
    {
        _streamsByHash[sharedStream.Hash] = sharedStream;
        _streamsById[sharedStream.SharedStreamId] = sharedStream;
        SharedStreams.Add(sharedStream);
    }

    public List<SharedStream> SharedStreams { get; set; } = new();
    public Dictionary<Guid, List<RelianceOnSharedStream>> Reliances => _reliances;

    public void AddReliance(RelianceOnSharedStream reliance)
    {
        lock (_reliancesLock)
        {
            if (!Reliances.TryGetValue(reliance.SharedStreamId, out var list))
            {
                list = [];
                Reliances[reliance.SharedStreamId] = list;
            }

            list.Add(reliance);
        }
    }

    public void RemoveReliance(RelianceOnSharedStream reliance)
    {
        SharedStream? toDispose = null;
        lock (_reliancesLock)
        {
            if (!Reliances.TryGetValue(reliance.SharedStreamId, out var reliance1)) return;
            reliance1.Remove(reliance);
            if (reliance1.Count != 0) return;
            Reliances.Remove(reliance.SharedStreamId);
            if (!_streamsById.Remove(reliance.SharedStreamId, out var sharedStream)) return;
            _streamsByHash.Remove(sharedStream.Hash);
            SharedStreams.Remove(sharedStream);
            toDispose = sharedStream;
        }

        toDispose?.Stream.Dispose();
    }
}

public static class SharedMemoryStreamPoolInstance
{
    public static readonly ISharedMemoryStreamPool Instance = new SharedMemoryStreamPool();
}

public class SharedStream
{
    public SharedStream(WrappedStream stream, string hash)
    {
        Stream = stream;
        Hash = hash;
        SharedStreamId = Guid.NewGuid();
    }

    public Guid SharedStreamId { get; internal set; }
    public string Hash { get; internal set; }
    public WrappedStream Stream { get; internal set; }
}

/// <summary>
/// THIS IS NOT A SECURE HASH, DO NOT USE IT FOR SECURITY
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// THIS IS NOT A SECURE HASH, DO NOT USE IT FOR SECURITY
    /// </summary>
    public static string HashByteArray(byte[] bytes)
    {
        var checksum = SHA256.HashData(bytes);
        return Convert.ToHexString(checksum);
    }

    /// <summary>
    /// THIS IS NOT A SECURE HASH, DO NOT USE IT FOR SECURITY
    /// </summary>
    public static string Hash(WrappedStream ws)
    {
        byte[] checksum = [];
        ws.Use(x => checksum = SHA256.HashData(x));
        return Convert.ToHexString(checksum);
    }
}

public class RelianceOnSharedStream : IDisposable
{
    private int _disposed;
    public Guid SharedStreamId { get; set; }
    public ISharedMemoryStreamPool Pool { get; set; }

    public RelianceOnSharedStream(SharedStream stream, ISharedMemoryStreamPool? pool = null)
    {
        SharedStreamId = stream.SharedStreamId;
        Pool = pool ?? SharedMemoryStreamPoolInstance.Instance;
        Pool.AddReliance(this);
    }

    public RelianceOnSharedStream(Guid sharedStreamId, ISharedMemoryStreamPool? pool = null)
    {
        SharedStreamId = sharedStreamId;
        Pool = pool ?? SharedMemoryStreamPoolInstance.Instance;
        Pool.AddReliance(this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Pool.RemoveReliance(this);
        }

        GC.SuppressFinalize(this);
    }
}