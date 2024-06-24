using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.WinformsApplication
{
    public delegate void OnEmitHandler(LogEvent logEvent);
}