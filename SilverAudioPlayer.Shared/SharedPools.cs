namespace SilverAudioPlayer.Shared
{
    public interface ISharedPool<T> where T : IDisposable
    {
        public bool ContainsHash(string hash);
        Dictionary<string, List<RelianceOnShared<T>>> Reliances { get; }
        public void AddReliance(RelianceOnShared<T> reliance);
        public void RemoveReliance(RelianceOnShared<T> reliance);
        public Shared<T>? GetWithHash(string hash);
        public Shared<T> AddT(T ws, string hash);
        public (Shared<T> Shared, RelianceOnShared<T> Reliance) AcquireOrAdd(T ws, string hash)
        {
            var shared = AddT(ws, hash);
            return (shared, new RelianceOnShared<T>(shared, this));
        }
    }

    public static class SharedMemoryTPoolInstance<T> where T : IDisposable
    {
        public static ISharedPool<T> Instance = new SharedPool<T>();
    }

    public class Shared<T>(T obj, string sharedId) where T : IDisposable
    {
        public string SharedId { get; internal set; } = sharedId;
        public T Object { get; internal set; } = obj;
    }

    public class SharedPool<T> : ISharedPool<T> where T : IDisposable
    {
        private readonly Dictionary<string, List<RelianceOnShared<T>>> _reliances = [];
        private readonly Dictionary<string, Shared<T>> _sharedById = new(StringComparer.Ordinal);
        private readonly Lock _reliancesLock = new();
        public Dictionary<string, List<RelianceOnShared<T>>> Reliances => _reliances;
        public List<Shared<T>> Shared { get; set; } = [];

        public void AddReliance(RelianceOnShared<T> reliance)
        {
            lock (_reliancesLock)
            {
                if (!Reliances.TryGetValue(reliance.SharedId, out List<RelianceOnShared<T>>? value))
                {
                    value = [];
                    Reliances[reliance.SharedId] = value;
                }

                value.Add(reliance);
            }
        }

        public void RemoveReliance(RelianceOnShared<T> reliance)
        {
            Shared<T>? toDispose = null;
            lock (_reliancesLock)
            {
                if (!Reliances.TryGetValue(reliance.SharedId, out var reliance1)) return;
                reliance1.Remove(reliance);
                if (reliance1.Count != 0) return;
                Reliances.Remove(reliance.SharedId);
                if (!_sharedById.Remove(reliance.SharedId, out var sharedStream)) return;
                Shared.Remove(sharedStream);
                toDispose = sharedStream;
            }

            toDispose?.Object.Dispose();
        }

        public bool ContainsHash(string hash)
        {
            lock (_reliancesLock)
            {
                return _sharedById.ContainsKey(hash);
            }
        }

        public Shared<T> AddT(T ws, string hash)
        {
            lock (_reliancesLock)
            {
                if (_sharedById.TryGetValue(hash, out var s))
                {
                    return s;
                }

                Shared<T> shared = new(ws, hash);
                _sharedById[hash] = shared;
                Shared.Add(shared);
                return shared;
            }
        }

        public (Shared<T> Shared, RelianceOnShared<T> Reliance) AcquireOrAdd(T ws, string hash)
        {
            lock (_reliancesLock)
            {
                if (!_sharedById.TryGetValue(hash, out var s))
                {
                    s = new Shared<T>(ws, hash);
                    _sharedById[hash] = s;
                    Shared.Add(s);
                }

                var reliance = new RelianceOnShared<T>(s, this);
                return (s, reliance);
            }
        }

        public Shared<T>? GetWithHash(string hash)
        {
            lock (_reliancesLock)
            {
                return _sharedById.GetValueOrDefault(hash);
            }
        }
    }

    public class RelianceOnShared<T> : IDisposable where T : IDisposable
    {
        private int _disposed;
        public ISharedPool<T> Pool { get; }

        public RelianceOnShared(Shared<T> shared, ISharedPool<T>? pool = null)
        {
            SharedId = shared.SharedId;
            Pool = pool ?? SharedMemoryTPoolInstance<T>.Instance;
            Pool.AddReliance(this);
        }

        public string SharedId { get; set; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Pool.RemoveReliance(this);
            }

            GC.SuppressFinalize(this);
        }
    }
}