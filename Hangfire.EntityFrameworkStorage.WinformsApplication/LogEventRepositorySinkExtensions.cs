using Serilog;
using Serilog.Configuration;

namespace Hangfire.EntityFrameworkStorage.WinformsApplication;

public static class LogEventRepositorySinkExtensions
{
    public static LoggerConfiguration LogEventRepositorySink(
        this LoggerSinkConfiguration loggerConfiguration, ILogEventEmitterService logEventEmitterService)
    {
        return loggerConfiguration.Sink(logEventEmitterService);
    }
}