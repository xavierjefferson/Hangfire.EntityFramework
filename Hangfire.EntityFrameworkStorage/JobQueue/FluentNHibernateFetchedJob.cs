using System;
using System.Globalization;
using System.Linq;
using Hangfire.Logging;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkStorage.JobQueue;

public class EntityFrameworkFetchedJob : IFetchedJob
{
    private static readonly ILog Logger = LogProvider.For<EntityFrameworkFetchedJob>();

    private readonly long _id;
    private readonly EntityFrameworkJobStorage _storage;
    private bool _disposed;
    private bool _removedFromQueue;
    private bool _requeued;

    public EntityFrameworkFetchedJob(
        EntityFrameworkJobStorage storage,
        FetchedJob fetchedJob)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _id = fetchedJob?.Id ?? throw new ArgumentNullException(nameof(fetchedJob));
        JobId = fetchedJob.JobId.ToString(CultureInfo.InvariantCulture);
        Queue = fetchedJob.Queue;
    }

    public string Queue { get; }

    public void Dispose()
    {
        if (_disposed) return;

        if (!_removedFromQueue && !_requeued) Requeue();

        _disposed = true;
    }

    public void RemoveFromQueue()
    {
        Logger.DebugFormat("RemoveFromQueue JobId={0}", JobId);
        _storage.UseDbContext(dbContext =>
        {
            dbContext.JobQueues.RemoveRange(dbContext.JobQueues.Where(i => i.Id == _id));
            dbContext.SaveChanges();
        });

        _removedFromQueue = true;
    }

    public void Requeue()
    {
        Logger.DebugFormat("Requeue JobId={0}", JobId);
        _storage.UseDbContext(dbContext =>
        {
            var tmp = dbContext.JobQueues.FirstOrDefault(i => i.Id == _id);
            if (tmp != null)
            {
                tmp.FetchedAt = null;
                dbContext.Update(tmp);
                dbContext.SaveChanges();
            }
        });

        _requeued = true;
    }

    public string JobId { get; }
}