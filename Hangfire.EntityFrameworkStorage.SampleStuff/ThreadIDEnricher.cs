using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.SampleStuff
{
    public class ThreadIDEnricher : ILogEventEnricher
    {
        public const string PropertyName = "ThreadID";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                PropertyName, Thread.CurrentThread.Name));
        }
    }
}