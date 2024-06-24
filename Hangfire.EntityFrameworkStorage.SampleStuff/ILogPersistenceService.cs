using System.Collections.Generic;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.SampleStuff;

public interface ILogPersistenceService
{
    List<LogItem> GetRecent();
    void Insert(LogEvent l);
}