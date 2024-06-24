using System.Collections.Generic;
using System.Linq;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.SampleStuff;

public class LogPersistenceService : ILogPersistenceService
{
    private readonly List<LogItem> _logItems = new();

    public List<LogItem> GetRecent()
    {
        return _logItems.OrderBy(i => i.dt).ToList();
        //using (var statelessSession = _sessionFactory.OpenStatelessSession())
        //{
        //    using (var transaction = statelessSession.BeginTransaction(IsolationLevel.Serializable))
        //    {
        //        return statelessSession.Query<LogItem>().OrderBy(i => i.dt).ToList();
        //    }
        //}
    }

    public void Insert(LogEvent logEvent)
    {
        _logItems.Add(new LogItem(logEvent));
        //using (var statelessSession = _sessionFactory.OpenStatelessSession())
        //{
        //    statelessSession.Insert(new LogItem (logEvent));
        //}
    }
}