using Serilog;

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
    class SharedPool<T> : ISharedPool<T> where T : IDisposable
    {
        private ILogger? _logger;
        public SharedPool()
        {
            _logger = Logger.GetLogger(typeof(SharedPool<T>));
        }
        private readonly Dictionary<string, List<RelianceOnShared<T>>> _reliances = [];
        private readonly object _reliancesLock = new();
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
            lock (_reliancesLock)
            {
                if (!Reliances.TryGetValue(reliance.SharedId, out var reliance1)) return;
                reliance1.Remove(reliance);
                if (reliance1.Count != 0) return;
                Reliances.Remove(reliance.SharedId);
                if (Shared.FirstOrDefault(x => x.SharedId == reliance.SharedId) is not
                    { } sharedStream) return;
                _logger?.Debug("remove str {RelianceSharedStreamId}", reliance.SharedId);
                sharedStream.Object.Dispose();
            }
        }
        public bool ContainsHash(string hash)
        {
           return Shared.Any(x=>x.SharedId== hash);
        }
        public Shared<T> AddT(T ws, string hash)
        {
            if (Shared.FirstOrDefault(x => x.SharedId == hash) is { } s)
            {
                return s;
            }

            Shared<T> shared = new(ws,hash);
            Shared.Add(shared); return shared;
        }
        public Shared<T>? GetWithHash(string hash)
        {
            if(Shared.FirstOrDefault(x=>x.SharedId==hash) is { }s)
            {
                return s;
            }
            return null;
        }

      
    }
    public class RelianceOnShared<T> : IDisposable where T : IDisposable
    {
        public RelianceOnShared(Shared<T> shared)
        {
            SharedId = shared.SharedId;
            SharedMemoryTPoolInstance<T>.Instance.AddReliance(this);
        }

        public string SharedId { get; set; }

        public void Dispose()
        {
            SharedMemoryTPoolInstance<T>.Instance.RemoveReliance(this);
            GC.SuppressFinalize(this);
        }
    }


}
