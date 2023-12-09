using Serilog;

namespace SilverAudioPlayer.Shared;

public static class Logger
{
    public static Func<Type, ILogger>? GetLoggerFunc
    {
        get => _GetLoggerFunc;
        set
        {
            if (_GetLoggerFunc == null)
            {
                _GetLoggerFunc = value;
            }
            else
            {
                throw new InvalidOperationException("You may only set the logger function once");
            }
        }
    }

    private static Func<Type, ILogger>? _GetLoggerFunc;

    public static ILogger? GetLogger(Type name)
    {
        return GetLoggerFunc?.Invoke(name);
    }

}