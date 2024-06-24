using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire.EntityFrameworkStorage.Entities;

namespace Hangfire.EntityFrameworkStorage.JobQueue
{
    public class EntityFrameworkJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private static readonly TimeSpan QueuesCacheTimeout = TimeSpan.FromSeconds(5);

        private readonly EntityFrameworkJobStorage _storage;
        private readonly object Mutex = new object();
        private DateTime _cacheUpdated;
        private List<string> _queuesCache = new List<string>();

        public EntityFrameworkJobQueueMonitoringApi(EntityFrameworkJobStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IEnumerable<string> GetQueues()
        {
            lock (Mutex)
            {
                if (_queuesCache.Count == 0 || _cacheUpdated.Add(QueuesCacheTimeout) < _storage.UtcNow)
                {
                    var result = _storage.UseStatelessSession(
                        dbContext => { return dbContext.JobQueues.Select(i => i.Queue).Distinct().ToList(); });

                    _queuesCache = result;
                    _cacheUpdated = _storage.UtcNow;
                }

                return _queuesCache.ToList();
            }
        }

        public IEnumerable<long> GetEnqueuedJobIds(string queue, int from, int perPage)
        {
            return _storage.UseStatelessSession(dbContext =>
            {
                return dbContext.JobQueues
                    .OrderBy(i => i.Id)
                    .Where(i => i.Queue == queue)
                    .Select(i => Convert.ToInt64(i.Job.Id))
                    .Skip(from)
                    .Take(perPage)
                    .ToList();
            });
        }


        public IEnumerable<long> GetFetchedJobIds(string queue, int from, int perPage)
        {
            //return Enumerable.Empty<long>();
            return _storage.UseStatelessSession(dbContext =>
            {
                return dbContext.JobQueues
                    .Where(i => (i.FetchedAt != null) & (i.Queue == queue))
                    .OrderBy(i => i.Id)
                    .Skip(from)
                    .Take(perPage)
                    .Select(i => Convert.ToInt64(i.Id))
                    .ToList();
            });
        }

        public EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue)
        {
            return _storage.UseStatelessSession(dbContext =>
            {
                var result = dbContext.JobQueues.Where(i => i.Queue == queue).Select(i => i.FetchedAt).ToList();

                return new EnqueuedAndFetchedCountDto
                {
                    EnqueuedCount = result.Count,
                    FetchedCount = result.Count(i => i != null)
                };
            });
        }
    }
}