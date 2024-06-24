using System;
using System.Threading;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkStorage.JobQueue;

public interface IPersistentJobQueue
{
    IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken);
    void Enqueue(HangfireContext dbContext, string queue, string jobId);
}