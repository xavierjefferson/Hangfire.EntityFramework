using System.Threading;
using Hangfire.Common;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkStorage;
#pragma warning disable 618
public class ServerTimeSyncManager : IBackgroundProcess, IServerComponent
{
    private readonly EntityFrameworkJobStorage _storage;

    public ServerTimeSyncManager(EntityFrameworkJobStorage storage)
    {
        _storage = storage;
    }

    public void Execute(BackgroundProcessContext context)
    {
        Execute(context.StoppedToken);
    }

    public void Execute(CancellationToken cancellationToken)
    {
        _storage.RefreshUtcOFfset();

        cancellationToken.Wait(_storage.Options.DbmsTimeSyncInterval);
    }
#pragma warning restore 618
}