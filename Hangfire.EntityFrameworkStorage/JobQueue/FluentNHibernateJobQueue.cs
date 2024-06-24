﻿using System;
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
        if (queues.Length == 0) throw new ArgumentException("Queue array must be non-empty.", "queues");
        Logger.Debug("Attempting to dequeue");

        EntityFrameworkFetchedJob fetchedJob = null;
        var timeoutSeconds = _storage.Options.InvisibilityTimeout.Negate().TotalSeconds;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fluentNHibernateDistributedLock = EntityFrameworkDistributedLock.Acquire(_storage, "JobQueue",
                    _storage.Options.JobQueueDistributedLockTimeout);
                using (fluentNHibernateDistributedLock)
                {
                    fetchedJob = SqlUtil.WrapForTransaction(() =>
                    {
                        return _storage.UseDbContextInTransaction(wrapper =>
                        {
                            var jobQueueFetchedAt = _storage.UtcNow;

                            var cutoff = jobQueueFetchedAt.AddSeconds(timeoutSeconds);
                            if (Logger.IsDebugEnabled())
                                Logger.Debug(string.Format("Getting jobs where {0}=null or {0}<{1}",
                                    nameof(_JobQueue.FetchedAt), cutoff));

                            var jobQueue = wrapper.JobQueues.Include(i => i.Job)
                                .FirstOrDefault(i =>
                                    (i.FetchedAt == null
                                     || i.FetchedAt < cutoff) && queues.Contains(i.Queue));
                            if (jobQueue != null)
                            {
                                jobQueue.FetchToken = Guid.NewGuid().ToString();
                                jobQueue.FetchedAt = jobQueueFetchedAt;

                                wrapper.Update(jobQueue);
                                wrapper.SaveChanges();

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

    public void Enqueue(HangfireContext wrapper, string queue, string jobId)
    {
        wrapper.Add(new _JobQueue
        {
            Job = wrapper.Jobs.SingleOrDefault(i => i.Id == jobId),
            Queue = queue
        });

        Logger.DebugFormat("Enqueued JobId={0} Queue={1}", jobId, queue);
    }
}