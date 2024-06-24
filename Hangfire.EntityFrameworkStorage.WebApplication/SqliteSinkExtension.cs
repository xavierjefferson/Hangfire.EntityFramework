using System;
using Hangfire.EntityFrameworkStorage.SampleStuff;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.WebApplication;

public static class SqliteSinkExtension
{
    public static LoggerConfiguration SqliteSink(
        this LoggerSinkConfiguration loggerConfiguration,
        LogEventLevel logEventLevel,
        ILogPersistenceService svc)


    {
        if (loggerConfiguration == null)
            throw new ArgumentNullException(nameof(loggerConfiguration));
        if (svc == null)
            throw new ArgumentNullException(nameof(svc));
        return loggerConfiguration.Sink(new SqliteSink(svc), logEventLevel);
    }
}