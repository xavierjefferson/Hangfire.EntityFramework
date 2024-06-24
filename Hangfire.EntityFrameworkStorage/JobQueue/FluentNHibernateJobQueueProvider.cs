using System;

namespace Hangfire.EntityFrameworkStorage.JobQueue;

internal class EntityFrameworkJobQueueProvider : IPersistentJobQueueProvider
{
    private readonly IPersistentJobQueue _jobQueue;
    private readonly IPersistentJobQueueMonitoringApi _monitoringApi;

    public EntityFrameworkJobQueueProvider(EntityFrameworkJobStorage storage)
    {
        if (storage == null) throw new ArgumentNullException(nameof(storage));
        _jobQueue = new EntityFrameworkJobQueue(storage);
        _monitoringApi = new EntityFrameworkJobQueueMonitoringApi(storage);
    }

    public IPersistentJobQueue GetJobQueue()
    {
        return _jobQueue;
    }

    public IPersistentJobQueueMonitoringApi GetJobQueueMonitoringApi()
    {
        return _monitoringApi;
    }
}