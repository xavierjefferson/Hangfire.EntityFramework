using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkJobStorageConnection : JobStorageConnection
{
    public const string HashDistributedLockName = "hash";
    private static readonly ILog Logger = LogProvider.For<EntityFrameworkJobStorageConnection>();

    public EntityFrameworkJobStorageConnection(EntityFrameworkJobStorage storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public EntityFrameworkJobStorage Storage { get; }

    public override IWriteOnlyTransaction CreateWriteTransaction()
    {
        return new EntityFrameworkWriteOnlyTransaction(Storage);
    }

    public override IDisposable AcquireDistributedLock(string resource, TimeSpan timeout)
    {
        return EntityFrameworkDistributedLock.Acquire(Storage, resource, timeout);
    }

    public override List<string> GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore,
        int count)
    {
        if (key == null && fromScore == 0 && toScore == 0 && count == 1) return new List<string>();

        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (count <= 0)
            throw new ArgumentException("The value must be a positive number", nameof(count));
        if (toScore < fromScore)
            throw new ArgumentException("The `toScore` value must be higher or equal to the `fromScore` value.",
                nameof(toScore));
        return Storage.UseDbContextInTransaction(wrapper =>
        {
            return wrapper.Sets.Where(i => i.Key == key && i.Score > fromScore && i.Score <= toScore)
                .OrderBy(i => i.Score).Select(i => i.Value).Take(count).ToList();
        });
    }

    public override string CreateExpiredJob(Job job, IDictionary<string, string> parameters, DateTime createdAt,
        TimeSpan expireIn)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        var invocationData = InvocationData.SerializeJob(job);

        Logger.DebugFormat("CreateExpiredJob={0}", SerializationHelper.Serialize(invocationData));

        return Storage.UseDbContextInTransaction(wrapper =>
        {
            var jobEntity = new _Job
            {
                InvocationData = SerializationHelper.Serialize(invocationData),
                Arguments = invocationData.Arguments,
                CreatedAt = createdAt,
                ExpireAt = createdAt.Add(expireIn)
            };
            wrapper.Add(jobEntity);
            wrapper.SaveChanges();
            //does nothing
            foreach (var keyValuePair in parameters)
            {
                wrapper.Add(new _JobParameter
                {
                    Job = jobEntity,
                    Name = keyValuePair.Key,
                    Value = keyValuePair.Value
                });
                wrapper.SaveChanges();
            }

            //does nothing
            return jobEntity.Id.ToString();
        });
    }

    public override IFetchedJob FetchNextJob(string[] queues, CancellationToken cancellationToken)
    {
        if (queues == null || queues.Length == 0) throw new ArgumentNullException(nameof(queues));

        var providers = queues
            .Select(queue => Storage.QueueProviders.GetProvider(queue))
            .Distinct()
            .ToArray();

        if (providers.Length != 1)
            throw new InvalidOperationException(string.Format(
                "Multiple provider instances registered for queues: {0}. You should choose only one type of persistent queues per server instance.",
                string.Join(", ", queues)));

        var persistentQueue = providers[0].GetJobQueue();
        return persistentQueue.Dequeue(queues, cancellationToken);
    }

    public override void SetJobParameter(string jobId, string name, string value)
    {
        if (jobId == null) throw new ArgumentNullException(nameof(jobId));
        if (name == null) throw new ArgumentNullException(nameof(name));

        Storage.UseDbContextInTransaction(context =>
        {
            var jobParameter = context.JobParameters.FirstOrDefault(i => i.Job.Id == jobId && i.Name == name);
            if (jobParameter != null)
            {
                jobParameter.Value = value;
                context.Update(jobParameter);
            }
            else
            {
                var job = context.Jobs.FirstOrDefault(i => i.Id == jobId);
                if (job != null)
                {
                    jobParameter = new _JobParameter { Job = job, Name = name, Value = value };
                    context.Add(jobParameter);
                }
            }

            context.SaveChanges();
        });
    }

    public override string GetJobParameter(string jobId, string name)
    {
        if (jobId == null) throw new ArgumentNullException(nameof(jobId));
        if (name == null) throw new ArgumentNullException(nameof(name));

        return Storage.UseStatelessSession(wrapper =>
            wrapper.JobParameters
                .Where(i => i.Job.Id == jobId && i.Name == name)
                .Select(i => i.Value)
                .SingleOrDefault());
    }

    public override JobData GetJobData(string jobId)
    {
        if (jobId == null) throw new ArgumentNullException(nameof(jobId));

        Logger.DebugFormat("Get job data for job '{0}'", jobId);

        return Storage.UseStatelessSession(wrapper =>
        {
            var jobData =
                wrapper.Set<_Job>()
                    .SingleOrDefault(i => i.Id == jobId);

            if (jobData == null) return null;
            var invocationData = SerializationHelper.Deserialize<InvocationData>(jobData.InvocationData);

            invocationData.Arguments = jobData.Arguments;

            Job job = null;
            JobLoadException loadException = null;

            try
            {
                job = invocationData.DeserializeJob();
            }
            catch (JobLoadException ex)
            {
                loadException = ex;
            }

            return new JobData
            {
                Job = job,
                State = jobData.StateName,
                CreatedAt = jobData.CreatedAt,
                LoadException = loadException
            };
        });
    }

    public override StateData GetStateData(string jobId)
    {
        if (jobId == null) throw new ArgumentNullException(nameof(Job));

        return Storage.UseStatelessSession(wrapper =>
        {
            var job = wrapper.Jobs
                .Where(i => i.Id == jobId)
                .Select(i => new { i.StateName, i.StateData, i.StateReason })
                .SingleOrDefault();
            if (job == null) return null;


            return new StateData
            {
                Name = job.StateName,
                Reason = job.StateReason,
                Data = new Dictionary<string, string>(
                    SerializationHelper.Deserialize<Dictionary<string, string>>(job.StateData),
                    StringComparer.OrdinalIgnoreCase)
            };
        });
    }

    public override void AnnounceServer(string serverId, ServerContext context)
    {
        if (serverId == null) throw new ArgumentNullException(nameof(serverId));
        if (context == null) throw new ArgumentNullException(nameof(context));

        Storage.UseDbContextInTransaction(wrapper =>
        {
            var data = SerializationHelper.Serialize(new ServerData
            {
                WorkerCount = context.WorkerCount,
                Queues = context.Queues,
                StartedAt = Storage.UtcNow
            });

            wrapper.UpsertEntity<_Server>(i => i.Id == serverId, server =>
            {
                server.LastHeartbeat = Storage.UtcNow;
                server.Data = data;
            }, server => { server.Id = serverId; });
        });
    }

    public override void RemoveServer(string serverId)
    {
        if (serverId == null) throw new ArgumentNullException(nameof(serverId));

        Storage.UseStatelessSession(wrapper =>
        {
            wrapper.RemoveRange(wrapper.Servers.Where(i => i.Id == serverId));
            wrapper.SaveChanges();
        });
    }

    public override void Heartbeat(string serverId)
    {
        if (serverId == null) throw new ArgumentNullException(nameof(serverId));

        Storage.UseStatelessSession(wrapper =>
        {
            var tmp = wrapper.Servers.FirstOrDefault(i => i.Id == serverId);
            if (tmp != null)
            {
                tmp.LastHeartbeat = Storage.UtcNow;
                wrapper.Update(tmp);
                wrapper.SaveChanges();
            }
        });
    }

    public override int RemoveTimedOutServers(TimeSpan timeOut)
    {
        if (timeOut.Duration() != timeOut)
            throw new ArgumentException(string.Format("The `{0}` value must be positive.", nameof(timeOut)),
                nameof(timeOut));

        return
            Storage.UseStatelessSession(wrapper =>
            {
                wrapper.RemoveRange(wrapper.Servers.Where(i =>
                    i.LastHeartbeat < Storage.UtcNow.Subtract(timeOut)));
                return wrapper.SaveChanges();
            });
    }

    public override long GetSetCount(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return
            Storage.UseStatelessSession(wrapper =>
                wrapper.Sets.Count(i => i.Key == key));
    }

    public override List<string> GetRangeFromSet(string key, int startingFrom, int endingAt)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return Storage.UseStatelessSession(wrapper =>
        {
            return wrapper.Sets
                .OrderBy(i => i.Id)
                .Where(i => i.Key == key)
                .Skip(startingFrom)
                .Take(endingAt - startingFrom + 1)
                .Select(i => i.Value)
                .ToList();
        });
    }

    public override HashSet<string> GetAllItemsFromSet(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return
            Storage.UseStatelessSession(wrapper =>
            {
                var result = wrapper.Sets
                    .Where(i => i.Key == key)
                    .OrderBy(i => i.Id)
                    .Select(i => i.Value)
                    .ToList();
                return new HashSet<string>(result);
            });
    }

    public override string GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (toScore < fromScore)
            throw new ArgumentException(string.Format("The `{0}` value must be higher or equal to the `{1}` value.",
                nameof(toScore), nameof(fromScore)));

        return
            Storage.UseStatelessSession(wrapper =>
                wrapper.Sets
                    .OrderBy(i => i.Score)
                    .Where(i => i.Key == key && i.Score >= fromScore && i.Score <= toScore)
                    .Select(i => i.Value)
                    .FirstOrDefault());
    }

    public override long GetCounter(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return
            Storage.UseStatelessSession(wrapper =>
            {
                //have to compensate for NH processing of sums when there are no matching results.
                var counterSum = 0;
                if (wrapper.Counters.Any(i => i.Key == key))
                    counterSum = wrapper.Counters.Where(i => i.Key == key).Sum(i => i.Value);
                var aggregatedCounterSum = 0;
                if (wrapper.AggregatedCounters.Any(i => i.Key == key))
                    aggregatedCounterSum = wrapper.AggregatedCounters.Where(i => i.Key == key)
                        .Sum(i => i.Value);

                return counterSum + aggregatedCounterSum;
            });
    }

    public override long GetHashCount(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return
            Storage.UseStatelessSession(wrapper =>
                wrapper.Hashes.Count(i => i.Key == key));
    }

    public override TimeSpan GetHashTtl(string key)
    {
        return GetTTL<_Hash>(key);
    }

    public override long GetListCount(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return
            Storage.UseStatelessSession(wrapper =>
                wrapper.Lists.Count(i => i.Key == key));
    }

    public override TimeSpan GetListTtl(string key)
    {
        return GetTTL<_List>(key);
    }

    public override string GetValueFromHash(string key, string name)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (name == null) throw new ArgumentNullException(nameof(name));

        return
            Storage.UseStatelessSession(wrapper =>
                wrapper.Hashes
                    .Where(i => i.Key == key && i.Field == name)
                    .Select(i => i.Value)
                    .SingleOrDefault());
    }

    public override List<string> GetRangeFromList(string key, int startingFrom, int endingAt)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return Storage.UseStatelessSession(wrapper =>
        {
            return
                wrapper.Lists
                    .OrderByDescending(i => i.Id)
                    .Where(i => i.Key == key)
                    .Select(i => i.Value)
                    .Skip(startingFrom)
                    .Take(endingAt - startingFrom + 1)
                    .ToList();
        });
    }

    public override List<string> GetAllItemsFromList(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return Storage.UseStatelessSession(wrapper =>
        {
            return
                wrapper.Lists
                    .OrderByDescending(i => i.Id)
                    .Where(i => i.Key == key)
                    .Select(i => i.Value)
                    .ToList();

            ;
        });
    }

    private TimeSpan GetTTL<T>(string key) where T : class, IExpirableWithKey
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return Storage.UseStatelessSession(wrapper =>
        {
            var item = wrapper.Set<T>().Where(i => i.Key == key).Min(i => i.ExpireAt);
            if (item == null) return TimeSpan.FromSeconds(-1);

            return item.Value - Storage.UtcNow;
        });
    }

    public override TimeSpan GetSetTtl(string key)
    {
        return GetTTL<_Set>(key);
    }

    public override void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (keyValuePairs == null) throw new ArgumentNullException(nameof(keyValuePairs));
        using (EntityFrameworkDistributedLock.Acquire(Storage, HashDistributedLockName, TimeSpan.FromSeconds(10)))
        {
            Storage.UseDbContextInTransaction(wrapper =>
            {
                foreach (var keyValuePair in keyValuePairs)
                    wrapper.UpsertEntity<_Hash>(i => i.Key == key && i.Field == keyValuePair.Key,
                        i => { i.Value = keyValuePair.Value; },
                        i =>
                        {
                            i.Key = key;
                            i.Field = keyValuePair.Key;
                        });
            });
        }
    }

    public override Dictionary<string, string> GetAllEntriesFromHash(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return Storage.UseStatelessSession(wrapper =>
        {
            var result = wrapper.Hashes
                .Where(i => i.Key == key)
                .ToDictionary(i => i.Field, i => i.Value);
            return result.Count != 0 ? result : null;
        });
    }
}