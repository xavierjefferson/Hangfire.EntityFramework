using System.Collections.Generic;

namespace Hangfire.EntityFrameworkStorage.JobQueue;

public interface IPersistentJobQueueMonitoringApi
{
    IEnumerable<string> GetQueues();
    IEnumerable<string> GetEnqueuedJobIds(string queue, int from, int perPage);
    IEnumerable<string> GetFetchedJobIds(string queue, int from, int perPage);
    EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue);
}