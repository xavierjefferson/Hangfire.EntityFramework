using Serilog.Core;

namespace Hangfire.EntityFrameworkStorage.WinformsApplication;

public interface ILogEventEmitterService : ILogEventSink
{
    event OnEmitHandler OnEmit;
}