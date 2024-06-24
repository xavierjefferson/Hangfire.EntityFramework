using System;
using Hangfire.EntityFrameworkStorage.SampleStuff;
using Serilog.Core;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.WebApplication;

public class SqliteSink : ILogEventSink

{
    private readonly ILogPersistenceService _logPersistenceService;

    public SqliteSink(
        ILogPersistenceService logPersistenceService)
    {
        _logPersistenceService = logPersistenceService;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));
        _logPersistenceService.Insert(logEvent);
    }
}