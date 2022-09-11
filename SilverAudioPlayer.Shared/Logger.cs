using Serilog;

namespace SilverAudioPlayer.Shared
{
    public static class Logger
    {
        public static event Func<Type, ILogger>? GetLoggerFunc;

        public static ILogger? GetLogger(Type name)
        {
            if (GetLoggerFunc != null)
                return GetLoggerFunc(name);
            return null;
        }
    }
}