using Serilog.Events;

namespace Hangfire.EntityFrameworkStorage.WinformsApplication;

public class LogEventEmitterService : ILogEventEmitterService
{
    public event OnEmitHandler OnEmit;

    public void Emit(LogEvent logEvent)
    {
        OnEmit?.Invoke(logEvent);
    }
}