using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using EntityFramework.Cfg;
using EntityFramework.Cfg.Db;
using Hangfire.Annotations;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Maps;
using Hangfire.EntityFrameworkStorage.Monitoring;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;
using Newtonsoft.Json;

using NHibernate.Metadata;
using NHibernate.Persister.Entity;
using Snork.EntityFrameworkTools;

namespace Hangfire.EntityFrameworkStorage
{
    public class EntityFrameworkJobStorage : JobStorage, IDisposable
    {
        private static readonly ILog Logger = LogProvider.For<EntityFrameworkJobStorage>();

        private CountersAggregator _countersAggregator;
        private bool _disposedValue;
        private ExpirationManager _expirationManager;
        private ServerTimeSyncManager _serverTimeSyncManager;
        private HangfireContext _sessionFactory;


        private TimeSpan _utcOffset = TimeSpan.Zero;

        public EntityFrameworkJobStorage(ProviderTypeEnum providerType, string nameOrConnectionString,
            EntityFrameworkStorageOptions options = null) : this(
            SessionFactoryBuilder.GetFromAssemblyOf<_CounterMap>(providerType, nameOrConnectionString,
                options ?? new EntityFrameworkStorageOptions()))
        {
        }


        public EntityFrameworkJobStorage(IPersistenceConfigurer persistenceConfigurer,
            EntityFrameworkStorageOptions options = null)
        {
            if (persistenceConfigurer == null) throw new ArgumentNullException(nameof(persistenceConfigurer));
            Initialize(SessionFactoryBuilder.GetFromAssemblyOf<_CounterMap>(
                persistenceConfigurer, options));
        }

        public EntityFrameworkJobStorage(SessionFactoryInfo info)
        {
            Initialize(info);
        }

        internal IDictionary<string, IClassMetadata> ClassMetadataDictionary { get; set; }
        internal SessionFactoryInfo SessionFactoryInfo { get; set; }

        public EntityFrameworkStorageOptions Options { get; set; }


        public virtual PersistentJobQueueProviderCollection QueueProviders { get; private set; }

        public ProviderTypeEnum ProviderType { get; set; } = ProviderTypeEnum.None;

        public DateTime UtcNow => DateTime.UtcNow.Add(_utcOffset);

        public void RefreshUtcOFfset()
        {
            using (var dbContext = SessionFactoryInfo.SessionFactory.OpenSession())
            {
                _utcOffset = dbContext.GetUtcOffset(ProviderType);
            }
        }

        private void Initialize(SessionFactoryInfo info)
        {
            SessionFactoryInfo = info ?? throw new ArgumentNullException(nameof(info));
            ClassMetadataDictionary = info.SessionFactory.GetAllClassMetadata();
            ProviderType = info.ProviderType;
            _sessionFactory = info.SessionFactory;

            var tmp = info.Options as EntityFrameworkStorageOptions;
            Options = tmp ?? new EntityFrameworkStorageOptions();

            InitializeQueueProviders();
            _expirationManager = new ExpirationManager(this);
            _countersAggregator = new CountersAggregator(this);
            _serverTimeSyncManager = new ServerTimeSyncManager(this);


            //escalate dbContext factory issues early
            try
            {
                EnsureDualHasOneRow();
            }
            catch (FluentConfigurationException ex)
            {
                throw ex.InnerException ?? ex;
            }

            RefreshUtcOFfset();
        }


        internal string GetTableName<T>() where T : class
        {
            string entityName;
            var fullName = typeof(T).FullName;
            if (ClassMetadataDictionary.ContainsKey(fullName))
            {
                var classMetadata = ClassMetadataDictionary[fullName] as SingleTableEntityPersister;
                entityName = classMetadata == null ? typeof(T).Name : classMetadata.TableName;
            }
            else
            {
                entityName = typeof(T).Name;
            }

            return entityName;
        }

        private void EnsureDualHasOneRow()
        {
            try
            {
                UseDbContextInTransaction(dbContext =>
                {
                    var count = dbContext.Duals.Count();
                    switch (count)
                    {
                        case 1:
                            return;
                        case 0:
                            dbContext.Insert(new _Dual {Id = 1});
                            break;
                        default:
                            dbContext.DeleteByInt32Id<_Dual>(
                                dbContext.Duals.Skip(1).Select(i => i.Id).ToList());
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.WarnException("Issue with dual table", ex);
                throw;
            }
        }

        private void InitializeQueueProviders()
        {
            QueueProviders =
                new PersistentJobQueueProviderCollection(
                    new EntityFrameworkJobQueueProvider(this));
        }


        public override void WriteOptionsToLog(ILog logger)
        {
            if (logger.IsInfoEnabled())
                logger.DebugFormat("Using the following options for job storage: {0}",
                    JsonConvert.SerializeObject(Options, Formatting.Indented));
        }


        public override IMonitoringApi GetMonitoringApi()
        {
            return new EntityFrameworkMonitoringApi(this);
        }

        public override IStorageConnection GetConnection()
        {
            return new EntityFrameworkJobStorageConnection(this);
        }


        internal T UseDbContextInTransaction<T>([InstantHandle] Func<HangfireContext, T> func)
        {
            using (var transaction = CreateTransaction())
            {
                var result = UseStatelessSession(func);
                transaction.Complete();
                return result;
            }
        }

        internal void UseDbContextInTransaction([InstantHandle] Action<HangfireContext> action)
        {
            UseDbContextInTransaction(statelessSessionWrapper =>
            {
                action(statelessSessionWrapper);
                return false;
            });
        }


        public TransactionScope CreateTransaction()
        {
            return new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = Options.TransactionIsolationLevel,
                    Timeout = Options.TransactionTimeout
                });
        }


        public void UseStatelessSession([InstantHandle] Action<HangfireContext> action)
        {
            using (var dbContext = GetStatelessSession())
            {
                action(dbContext);
            }
        }

        public T UseStatelessSession<T>([InstantHandle] Func<HangfireContext, T> func)
        {
            using (var dbContext = GetStatelessSession())
            {
                return func(dbContext);
            }
        }

        public HangfireContext GetStatelessSession()
        {
            var statelessSession = _sessionFactory.OpenStatelessSession();
            return new HangfireContext(statelessSession, this);
        }

#pragma warning disable 618
        public List<IBackgroundProcess> GetBackgroundProcesses()
        {
            return new List<IBackgroundProcess> {_expirationManager, _countersAggregator, _serverTimeSyncManager};
        }

        public override IEnumerable<IServerComponent> GetComponents()

        {
            return new List<IServerComponent> {_expirationManager, _countersAggregator, _serverTimeSyncManager};
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                    if (_sessionFactory != null)
                    {
                        try
                        {
                            if (!_sessionFactory.IsClosed)
                                _sessionFactory.Close();
                        }
                        catch
                        {
                            // ignored
                        }

                        _sessionFactory.Dispose();
                        _sessionFactory = null;
                    }
                // TODO: dispose managed state (managed objects)

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~EntityFrameworkJobStorage()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#pragma warning restore 618
    }
}