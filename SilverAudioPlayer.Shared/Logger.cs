using Serilog;

namespace SilverAudioPlayer.Shared;

public static class Logger
{
    public static Func<Type, ILogger>? GetLoggerFunc
    {
        get => _GetLoggerFunc;
        set
        {
            _GetLoggerFunc = value;
            OnNewLoggerSet?.Invoke(value);
        }
    }

    private static Func<Type, ILogger>? _GetLoggerFunc;

    public static ILogger? GetLogger(Type name)
    {
        return GetLoggerFunc?.Invoke(name);
    }

    public static event Action<Func<Type, ILogger>?> OnNewLoggerSet;
}