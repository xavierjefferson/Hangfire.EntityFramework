using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.Interfaces;
using Hangfire.Logging;
using Hangfire.States;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkWriteOnlyTransaction : JobStorageTransaction
{
    private static readonly ILog Logger = LogProvider.For<EntityFrameworkWriteOnlyTransaction>();


    //transactional command queue.
    private readonly Queue<Action<HangfireContext>> _commandQueue = new();

    private readonly EntityFrameworkJobStorage _storage;

    public EntityFrameworkWriteOnlyTransaction(EntityFrameworkJobStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    private void SetExpireAt<T>(string key, DateTime? expire, HangfireContext dbContext)
        where T : EntityBase, IExpirableWithKey
    {
        var tmp = dbContext.Set<T>().FirstOrDefault(i => i.Key == key);
        if (tmp != null)
        {
            tmp.ExpireAt = expire.ToEpochDate();
            dbContext.Update(tmp);
            dbContext.SaveChanges();
        }
    }

    private void DeleteByKey<T>(string key, HangfireContext dbContext) where T : EntityBase, IExpirableWithKey
    {
        dbContext.RemoveRange(dbContext.Set<T>().Where(i => i.Key == key));
        dbContext.SaveChanges();
        //does nothing
    }

    private void DeleteByKeyValue<T>(string key, string value, HangfireContext dbContext)
        where T : class, IKeyWithStringValue
    {
        dbContext.RemoveRange(dbContext.Set<T>().Where(i => i.Key == key && i.Value == value));
        dbContext.SaveChanges();
        //does nothing
    }

    public override void ExpireJob(string jobId, TimeSpan expireIn)
    {
        Logger.DebugFormat("ExpireJob jobId={0}", jobId);

        AcquireJobLock();

        AddCommand(dbContext =>
        {
            var job = dbContext.Jobs.FirstOrDefault(i => i.Id == jobId);
            if (job != null)
            {
                job.ExpireAt = _storage.UtcNow.Add(expireIn).ToEpochDate();
                dbContext.Update(job);
                dbContext.SaveChanges();
            }
        });
    }

    public override void PersistJob(string jobId)
    {
        Logger.DebugFormat("PersistJob jobId={0}", jobId);

        AcquireJobLock();

        AddCommand(dbContext =>
        {
            var tmp = dbContext.Jobs.FirstOrDefault(i => i.Id == jobId);
            if (tmp != null)
            {
                tmp.ExpireAt = null;
                dbContext.Update(tmp);
                dbContext.SaveChanges();
            }
        });
    }

    public override void SetJobState(string jobId, IState state)
    {
        Logger.DebugFormat("SetJobState jobId={0}", jobId);

        AcquireStateLock();
        AcquireJobLock();
        AddCommand(dbContext =>
        {
            var job = dbContext.Jobs.SingleOrDefault(i => i.Id == jobId);
            if (job != null)
            {
                var jobState = new _JobState
                {
                    Job = job,
                    Reason = state.Reason,
                    Name = state.Name,
                    CreatedAt = _storage.UtcNow.ToEpochDate(),
                    Data = SerializationHelper.Serialize(state.SerializeData())
                };
                dbContext.JobStates.Add(jobState);

                job.StateData = jobState.Data;
                job.StateReason = jobState.Reason;
                job.StateName = jobState.Name;
                job.LastStateChangedAt = _storage.UtcNow.ToEpochDate();

                dbContext.Update(job);
                dbContext.SaveChanges();
                //does nothing
            }
        });
    }

    public override void AddJobState(string jobId, IState state)
    {
        Logger.DebugFormat("AddJobState jobId={0}, state={1}", jobId, state);

        AcquireStateLock();
        AddCommand(dbContext =>
        {
            var job = dbContext.Jobs.SingleOrDefault(i => i.Id == jobId);
            if (job != null)
                dbContext.Add(new _JobState
                {
                    Job = job,
                    Name = state.Name,
                    Reason = state.Reason,
                    CreatedAt = _storage.UtcNow.ToEpochDate(),
                    Data = SerializationHelper.Serialize(state.SerializeData())
                });

            dbContext.SaveChanges();
        });
    }

    public override void AddToQueue(string queue, string jobId)
    {
        Logger.DebugFormat("AddToQueue jobId={0}", jobId);

        var provider = _storage.QueueProviders.GetProvider(queue);
        var persistentQueue = provider.GetJobQueue();

        AddCommand(dbContext => persistentQueue.Enqueue(dbContext, queue, jobId));
    }

    public override void IncrementCounter(string key)
    {
        InsertCounter(key, 1);
    }


    public override void IncrementCounter(string key, TimeSpan expireIn)
    {
        InsertCounter(key, 1, expireIn);
    }

    private void InsertCounter(string key, int value, TimeSpan? expireIn = null)
    {
        Logger.DebugFormat("InsertCounter key={0}, expireIn={1}", key, expireIn);

        AcquireCounterLock();
        AddCommand(dbContext =>
        {
            dbContext.Add(new _Counter
            {
                Key = key,
                Value = value,
                ExpireAt = expireIn == null ? null : _storage.UtcNow.Add(expireIn.Value).ToEpochDate()
            });
            dbContext.SaveChanges();
        });
    }

    public override void DecrementCounter(string key)
    {
        InsertCounter(key, -1);
    }

    public override void DecrementCounter(string key, TimeSpan expireIn)
    {
        InsertCounter(key, -1, expireIn);
    }

    public override void AddToSet(string key, string value)
    {
        AddToSet(key, value, 0.0);
    }

    public override void AddToSet(string key, string value, double score)
    {
        Logger.DebugFormat("AddToSet key={0} value={1}", key, value);

        AcquireSetLock();
        AddCommand(dbContext =>
        {
            dbContext.UpsertEntity<_Set>(i => i.Key == key && i.Value == value, i => i.Score = score, i =>
            {
                i.Key = key;
                i.Value = value;
            });
        });
    }

    public override void AddRangeToSet(string key, IList<string> items)
    {
        Logger.DebugFormat("AddRangeToSet key={0}", key);

        if (key == null) throw new ArgumentNullException(nameof(key));
        if (items == null) throw new ArgumentNullException(nameof(items));

        AcquireSetLock();
        AddCommand(dbContext =>
        {
            foreach (var i in items) dbContext.Add(new _Set { Key = key, Value = i, Score = 0 });
            dbContext.SaveChanges();
        });
    }


    public override void RemoveFromSet(string key, string value)
    {
        Logger.DebugFormat("RemoveFromSet key={0} value={1}", key, value);

        AcquireSetLock();
        AddCommand(dbContext => { DeleteByKeyValue<_Set>(key, value, dbContext); });
    }

    public override void ExpireSet(string key, TimeSpan expireIn)
    {
        Logger.DebugFormat("ExpireSet key={0} expirein={1}", key, expireIn);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireSetLock();
        AddCommand(dbContext => { SetExpireAt<_Set>(key, _storage.UtcNow.Add(expireIn), dbContext); });
    }

    public override void InsertToList(string key, string value)
    {
        Logger.DebugFormat("InsertToList key={0} value={1}", key, value);

        AcquireListLock();
        AddCommand(dbContext =>
        {
            dbContext.Add(new _List { Key = key, Value = value });
            dbContext.SaveChanges();
        });
    }


    public override void ExpireList(string key, TimeSpan expireIn)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        Logger.DebugFormat("ExpireList key={0} expirein={1}", key, expireIn);

        AcquireListLock();
        AddCommand(dbContext => { SetExpireAt<_List>(key, _storage.UtcNow.Add(expireIn), dbContext); });
    }

    public override void RemoveFromList(string key, string value)
    {
        Logger.DebugFormat("RemoveFromList key={0} value={1}", key, value);

        AcquireListLock();
        AddCommand(dbContext => { DeleteByKeyValue<_List>(key, value, dbContext); });
    }

    public override void TrimList(string key, int keepStartingFrom, int keepEndingAt)
    {
        Logger.DebugFormat("TrimList key={0} from={1} to={2}", key, keepStartingFrom, keepEndingAt);

        AcquireListLock();
        AddCommand(dbContext =>
        {
            var idList = dbContext.Lists
                .OrderBy(i => i.Id)
                .Where(i => i.Key == key).ToList()
                .Select((i, j) => new { index = j, id = i.Id }).ToList();
            var before = idList.Where(i => i.index < keepStartingFrom || i.index > keepEndingAt)
                .Select(i => i.id)
                .ToList();
            dbContext.DeleteById<_List, int>(before);
        });
    }

    public override void PersistHash(string key)
    {
        Logger.DebugFormat("PersistHash key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireHashLock();
        AddCommand(dbContext => { SetExpireAt<_Hash>(key, null, dbContext); });
    }

    public override void PersistSet(string key)
    {
        Logger.DebugFormat("PersistSet key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireSetLock();
        AddCommand(dbContext => { SetExpireAt<_Set>(key, null, dbContext); });
    }

    public override void RemoveSet(string key)
    {
        Logger.DebugFormat("RemoveSet key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireSetLock();
        AddCommand(dbContext => { DeleteByKey<_Set>(key, dbContext); });
    }

    public override void PersistList(string key)
    {
        Logger.DebugFormat("PersistList key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireListLock();
        AddCommand(dbContext => { SetExpireAt<_List>(key, null, dbContext); });
    }

    public override void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs)
    {
        Logger.DebugFormat("SetRangeInHash key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));
        if (keyValuePairs == null) throw new ArgumentNullException(nameof(keyValuePairs));

        AcquireHashLock();
        AddCommand(dbContext =>
        {
            foreach (var keyValuePair in keyValuePairs)
                dbContext.UpsertEntity<_Hash>(i => i.Key == key && i.Name == keyValuePair.Key,
                    i => i.Value = keyValuePair.Value,
                    i =>
                    {
                        i.Name = keyValuePair.Key;
                        i.Key = key;
                    }
                );
        });
    }

    public override void ExpireHash(string key, TimeSpan expireIn)
    {
        Logger.DebugFormat("ExpireHash key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireHashLock();
        AddCommand(dbContext => { SetExpireAt<_Hash>(key, _storage.UtcNow.Add(expireIn), dbContext); });
    }

    public override void RemoveHash(string key)
    {
        Logger.DebugFormat("RemoveHash key={0} ", key);

        if (key == null) throw new ArgumentNullException(nameof(key));

        AcquireHashLock();
        AddCommand(dbContext => { DeleteByKey<_Hash>(key, dbContext); });
    }

    public override void Commit()
    {
        _storage.UseDbContextInTransaction(dbContext =>
        {
            foreach (var command in _commandQueue) command(dbContext);
            //does nothing
        });
    }

    internal void AddCommand(Action<HangfireContext> action)
    {
        _commandQueue.Enqueue(action);
    }

    private void AcquireJobLock()
    {
        AcquireLock("Job");
    }

    private void AcquireSetLock()
    {
        AcquireLock("Set");
    }

    private void AcquireListLock()
    {
        AcquireLock("List");
    }

    private void AcquireHashLock()
    {
        AcquireLock("Hash");
    }

    private void AcquireStateLock()
    {
        AcquireLock("State");
    }

    private void AcquireCounterLock()
    {
        AcquireLock("Counter");
    }

    private void AcquireLock(string resource)
    {
    }
}