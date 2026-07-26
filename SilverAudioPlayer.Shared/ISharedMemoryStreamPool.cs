using System.Security.Cryptography;
using Serilog;

namespace SilverAudioPlayer.Shared;

/// <summary>
/// A stream pool, a hashtable containing streams and reliance on those streams
/// </summary>
public interface ISharedMemoryStreamPool
{
    public SharedStream GetFromByteArray(byte[] bytes);
    public SharedStream GetFromWrappedStream(WrappedStream ws);
    Dictionary<Guid, List<RelianceOnSharedStream>> Reliances { get; }
    public void AddReliance(RelianceOnSharedStream reliance);
    public void RemoveReliance(RelianceOnSharedStream reliance);
}

public class SharedMemoryStreamPool : ISharedMemoryStreamPool
{
    private Dictionary<Guid, List<RelianceOnSharedStream>> _reliances = new();
    private readonly object _reliancesLock = new object();

    public SharedStream GetFromByteArray(byte[] bytes)
    {
        var hash = HashHelper.HashByteArray(bytes);
        lock (_reliancesLock)
        {
            if (SharedStreams.FirstOrDefault(x => x.Hash == hash) is { } sharedStream)
                return sharedStream;
            
            var memoryStream = new WrappedSharedMemoryStream(bytes);
            var sharedStream2 = new SharedStream(memoryStream, hash);

            SharedStreams.Add(sharedStream2);
            return sharedStream2;
        }
    }

    public SharedStream GetFromWrappedStream(WrappedStream ws) //TODO: group by path if wrappedfilestream
    {
        lock (_reliancesLock)
        {
            var hash = HashHelper.Hash(ws);
            var sharedStream2 = new SharedStream(ws, hash);
            if (SharedStreams.FirstOrDefault(x => x.Hash == hash) is { } sharedStream)
            {
                return sharedStream;
            }

            SharedStreams.Add(sharedStream2);
            return sharedStream2;
        }
    }

    public List<SharedStream> SharedStreams { get; set; } = new();
    public Dictionary<Guid, List<RelianceOnSharedStream>> Reliances => _reliances;

    public void AddReliance(RelianceOnSharedStream reliance)
    {
        lock (_reliancesLock)
        {
            if (!Reliances.ContainsKey(reliance.SharedStreamId))
            {
                Reliances[reliance.SharedStreamId] = new List<RelianceOnSharedStream>();
            }

            Reliances[reliance.SharedStreamId].Add(reliance);
        }
    }

    public void RemoveReliance(RelianceOnSharedStream reliance)
    {
        lock (_reliancesLock)
        {
            if (!Reliances.TryGetValue(reliance.SharedStreamId, out var reliance1)) return;
            reliance1.Remove(reliance);
            if (reliance1.Count != 0) return;
            Reliances.Remove(reliance.SharedStreamId);
            if (SharedStreams.FirstOrDefault(x => x.SharedStreamId == reliance.SharedStreamId) is not
                { } sharedStream) return;
            sharedStream.Stream.Dispose();
        }
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
        var sha = SHA256.Create();
        var checksum = sha.ComputeHash(bytes);
        return BitConverter.ToString(checksum).Replace("-", string.Empty);
    }
    /// <summary>
    /// THIS IS NOT A SECURE HASH, DO NOT USE IT FOR SECURITY
    /// </summary>
    public static string Hash(WrappedStream ws)
    {
        var sha = SHA256.Create();
        byte[]? checksum = null;
        ws.Use(x => checksum = sha.ComputeHash(x));
        return BitConverter.ToString(checksum).Replace("-", string.Empty);
    }
}

public class RelianceOnSharedStream : IDisposable
{
    public RelianceOnSharedStream(SharedStream stream)
    {
        SharedStreamId = stream.SharedStreamId;
        SharedMemoryStreamPoolInstance.Instance.AddReliance(this);
    }

    public Guid SharedStreamId { get; set; }

    public void Dispose()
    {
        SharedMemoryStreamPoolInstance.Instance.RemoveReliance(this);
    }
}