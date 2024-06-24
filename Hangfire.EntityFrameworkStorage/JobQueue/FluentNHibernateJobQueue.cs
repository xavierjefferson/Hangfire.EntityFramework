using System;
using System.Linq;
using System.Threading;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.Logging;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkStorage.JobQueue;

public class EntityFrameworkJobQueue : IPersistentJobQueue
{
    private static readonly ILog Logger = LogProvider.For<EntityFrameworkJobQueue>();

    private readonly EntityFrameworkJobStorage _storage;

    public EntityFrameworkJobQueue(EntityFrameworkJobStorage storage)
    {
        Logger.Debug("Job queue initialized");
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken)
    {
        if (queues == null) throw new ArgumentNullException(nameof(queues));
        if (queues.Length == 0) throw new ArgumentException("Queue array must be non-empty.", nameof(queues));
        Logger.Debug("Attempting to dequeue");

        EntityFrameworkFetchedJob fetchedJob = null;
        var timeoutSeconds = _storage.Options.InvisibilityTimeout.Negate().TotalSeconds;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var myLock = EntityFrameworkDistributedLock.Acquire(_storage, "JobQueue",
                    _storage.Options.JobQueueDistributedLockTimeout);
                using (myLock)
                {
                    fetchedJob = SqlUtil.WrapForTransaction(() =>
                    {
                        return _storage.UseDbContextInTransaction(dbContext =>
                        {
                            var jobQueueFetchedAt = _storage.UtcNow;

                            var cutoff = jobQueueFetchedAt.AddSeconds(timeoutSeconds);
                            if (Logger.IsDebugEnabled())
                                Logger.Debug(string.Format("Getting jobs where {0}=null or {0}<{1}",
                                    nameof(_JobQueue.FetchedAt), cutoff));

                            var jobQueue = dbContext.JobQueues.Include(i => i.Job).OrderBy(i => i.Id)
                                .FirstOrDefault(i =>
                                    (i.FetchedAt == null
                                     || i.FetchedAt < cutoff.ToEpochDate()) && queues.Contains(i.Queue));
                            if (jobQueue != null)
                            {
                                jobQueue.FetchToken = Guid.NewGuid().ToString();
                                jobQueue.FetchedAt = jobQueueFetchedAt.ToEpochDate();

                                dbContext.Update(jobQueue);
                                dbContext.SaveChanges();

                                Logger.DebugFormat("Dequeued job id {0} from queue {1}",
                                    jobQueue.Job.Id,
                                    jobQueue.Queue);
                                var tmp = new FetchedJob
                                {
                                    Id = jobQueue.Id,
                                    JobId = jobQueue.Job.Id,
                                    Queue = jobQueue.Queue
                                };
                                return new EntityFrameworkFetchedJob(_storage, tmp);
                            }


                            return null;
                        });
                    });
                }

                if (fetchedJob != null) return fetchedJob;
            }
            catch (DistributedLockTimeoutException)
            {
                Logger.Debug("Distributed lock acquisition timeout was exceeded");
            }

            cancellationToken.PollForCancellation(_storage.Options.QueuePollInterval);
        } while (fetchedJob == null);

        return fetchedJob;
    }

    public void Enqueue(HangfireContext dbContext, string queue, string jobId)
    {
        dbContext.Add(new _JobQueue
        {
            Job = dbContext.Jobs.SingleOrDefault(i => i.Id == jobId),
            Queue = queue
        });
        dbContext.SaveChanges();
        Logger.DebugFormat("Enqueued JobId={0} Queue={1}", jobId, queue);
    }
}