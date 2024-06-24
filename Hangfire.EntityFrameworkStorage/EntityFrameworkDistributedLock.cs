using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.Logging;
using Hangfire.Storage;


namespace Hangfire.EntityFrameworkStorage
{
    public class EntityFrameworkDistributedLock : IDisposable, IComparable
    {
        private static readonly object Mutex = new object();
        private static readonly ILog Logger = LogProvider.For<EntityFrameworkDistributedLock>();
        private readonly CancellationToken _cancellationToken;
        private readonly EntityFrameworkStorageOptions _options;
        private readonly string _resource;
        private readonly EntityFrameworkJobStorage _storage;
        private readonly TimeSpan _timeout;
        private int? _lockId;

        private EntityFrameworkDistributedLock(EntityFrameworkJobStorage storage, string resource, TimeSpan timeout,
            CancellationToken? cancellationToken = null)
        {
            Logger.DebugFormat("{2} resource={0}, timeout={1}", resource, timeout, GetType().Name);

            _resource = resource;
            _timeout = timeout;
            _cancellationToken = cancellationToken ?? new CancellationToken();
            _storage = storage;
            _options = storage.Options;
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            var distributedLock = obj as EntityFrameworkDistributedLock;
            if (distributedLock != null)
                return string.Compare(_resource, distributedLock._resource,
                    StringComparison.InvariantCultureIgnoreCase);

            throw new ArgumentException(string.Format("Object is not a {0}", GetType().Name));
        }

        public void Dispose()
        {
            if (_lockId.HasValue)
                Release();
        }

        public static EntityFrameworkDistributedLock Acquire(EntityFrameworkJobStorage storage, string resource,
            TimeSpan timeout,
            CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentNullException(nameof(resource));

            var tmp = new EntityFrameworkDistributedLock(storage, resource, timeout, cancellationToken);
            tmp.Initialize();
            return tmp;
        }

        private T TryMutex<T>(Func<T> funcTaken, Func<T> notTaken)
        {
            var timeout = _options.QueuePollInterval;
            var lockTaken = false;

            try
            {
                Monitor.TryEnter(Mutex, timeout, ref lockTaken);
                if (lockTaken)
                    return funcTaken();
                else
                    return notTaken();
            }
            finally
            {
                // Ensure that the lock is released.
                if (lockTaken) Monitor.Exit(Mutex);
            }
        }

        internal void Initialize()
        {
            var started = Stopwatch.StartNew();

            do
            {
                var acquired = SqlUtil.WrapForTransaction(() => SqlUtil.WrapForDeadlock(_cancellationToken, () =>
                {
                    var done = TryMutex(() =>
                    {
                        var result = _storage.UseDbContextInTransaction(TryAcquireWithEntity);
                        return result;
                    }, () => false);


                    return done;
                }, _options));

                if (acquired)
                {
                    if (Logger.IsDebugEnabled())
                        Logger.DebugFormat("Granted distributed lock '{0}'", _resource);
                    return;
                }

                if (started.Elapsed > _options.DistributedLockWaitTimeout) break;
                if (Logger.IsDebugEnabled())
                    Logger.Debug(
                        $"Will poll for distributed lock '{_resource}' in {_options.DistributedLockPollInterval}.");
                _cancellationToken.PollForCancellation(_options.DistributedLockPollInterval);
            } while (true);

            //dont change this.  Hangfire looks for resource name in exception properties
            throw new DistributedLockTimeoutException(_resource);
        }

        private bool TryAcquireWithEntity(HangfireContext dbContext)
        {
            var distributedLock = dbContext.DistributedLocks()
                .FirstOrDefault(i => i.Resource == _resource);

            var utcNow = dbContext.Storage.UtcNow;
            var expireAtAsLong = utcNow.Add(_timeout).ToEpochDate();
            if (distributedLock == null)
            {
                distributedLock = new _DistributedLock
                {
                    CreatedAt = utcNow,
                    Resource = _resource,
                    ExpireAtAsLong = expireAtAsLong
                };
                dbContext.Insert(distributedLock);

                _lockId = distributedLock.Id;

                if (Logger.IsDebugEnabled())
                    Logger.Debug($"Inserted row for distributed lock '{_resource}'");
                return true;
            }

            if (distributedLock.ExpireAtAsLong < utcNow.ToEpochDate())
            {
                distributedLock.CreatedAt = utcNow;
                distributedLock.ExpireAtAsLong = expireAtAsLong;
                dbContext.UpdateAndFlush(distributedLock);
                if (Logger.IsDebugEnabled())
                    Logger.Debug($"Re-used row for distributed lock '{_resource}'");
                _lockId = distributedLock.Id;
                return true;
            }

            return false;
        }


        internal void Release()
        {
            SqlUtil.WrapForTransaction(() =>
            {
                SqlUtil.WrapForDeadlock(_cancellationToken, () =>
                {
                    lock (Mutex)
                    {
                        if (_lockId.HasValue)
                            _storage.UseStatelessSession(dbContext =>
                            {
                               dbContext.RemoveRange(dbContext.DistributedLocks.Where(i => i.Id == _lockId));
                               dbContext.SaveChanges();
                                Logger.DebugFormat("Released distributed lock for {0}", _resource);
                                _lockId = null;
                            });
                    }
                }, _options);
            });
        }
    }
}