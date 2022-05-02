using Serilog;
using Serilog.Configuration;

namespace Silver.Serilog.MSAppCenterSink
{
    public static class MSAppCenterSinkExtensions
    {
        public static LoggerConfiguration MSAppCenter(
                  this LoggerSinkConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Sink(new MSAppCenterSink());
        }
    }
}