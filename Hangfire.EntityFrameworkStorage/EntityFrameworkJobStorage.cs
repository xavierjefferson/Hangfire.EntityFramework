using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Hangfire.Annotations;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Monitoring;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkJobStorage : JobStorage, IDisposable
{
    private static readonly ILog Logger = LogProvider.For<EntityFrameworkJobStorage>();
    private readonly IServiceProvider _serviceProvider;


    private readonly TimeSpan _utcOffset = TimeSpan.Zero;

    private CountersAggregator _countersAggregator;
    private bool _disposedValue;
    private ExpirationManager _expirationManager;
    private ServerTimeSyncManager _serverTimeSyncManager;

    public EntityFrameworkJobStorage(Action<DbContextOptionsBuilder> dbContextOptionsBuilder,
        EntityFrameworkStorageOptions options = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<HangfireContext>(i =>
        {
            dbContextOptionsBuilder(i);
            i.ConfigureWarnings(x => x.Ignore(RelationalEventId.AmbientTransactionWarning));
        });
        _serviceProvider = serviceCollection.BuildServiceProvider();
        Initialize(options);
    }

    public EntityFrameworkStorageOptions Options { get; set; }


    public virtual PersistentJobQueueProviderCollection QueueProviders { get; private set; }


    public DateTime UtcNow => DateTime.UtcNow.Add(_utcOffset);

    public HangfireContext GetDbContext()
    {
        var a = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<HangfireContext>();
        return a;
    }

    public void RefreshUtcOFfset()
    {
        //using (var dbContext = serviceProvider.GetRequiredService<HangfireContext>())
        //{
        //    _utcOffset = dbContext.GetUtcOffset(ProviderType);
        //}
    }

    private void Initialize(EntityFrameworkStorageOptions options)
    {
        Options = options ?? new EntityFrameworkStorageOptions();
        _serviceProvider.GetRequiredService<HangfireContext>().Database.EnsureCreated();
        InitializeQueueProviders();
        _expirationManager = new ExpirationManager(this);
        _countersAggregator = new CountersAggregator(this);
        _serverTimeSyncManager = new ServerTimeSyncManager(this);


        //escalate dbContext factory issues early
        try
        {
            EnsureDualHasOneRow();
        }
        catch (Exception ex)
        {
            throw ex.InnerException ?? ex;
        }

        RefreshUtcOFfset();
    }

    private void EnsureDualHasOneRow()
    {
        try
        {
            UseDbContextInTransaction(wrapper =>
            {
                var count = wrapper.Duals.Count();
                switch (count)
                {
                    case 1:
                        return;
                    case 0:
                        wrapper.Add(new _Dual { Id = 1 });
                        wrapper.SaveChanges();
                        break;
                    default:
                        wrapper.DeleteById<_Dual, int>(
                            wrapper.Duals.Skip(1).Select(i => i.Id).ToList());
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
        using (var dbContext = GetDbContext())
        {
            action(dbContext);
        }
    }

    public T UseStatelessSession<T>([InstantHandle] Func<HangfireContext, T> func)
    {
        using (var dbContext = GetDbContext())
        {
            return func(dbContext);
        }
    }

#pragma warning disable 618
    public List<IBackgroundProcess> GetBackgroundProcesses()
    {
        return new List<IBackgroundProcess> { _expirationManager, _countersAggregator, _serverTimeSyncManager };
    }

    public override IEnumerable<IServerComponent> GetComponents()

    {
        return new List<IServerComponent> { _expirationManager, _countersAggregator, _serverTimeSyncManager };
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
            if (disposing)
                //if (_sessionFactory != null)
                //{
                //    try
                //    {
                //        if (!_sessionFactory.IsClosed)
                //            _sessionFactory.Close();
                //    }
                //    catch
                //    {
                //        // ignored
                //    }
                //    _sessionFactory.Dispose();
                //    _sessionFactory = null;
                //}
                // TODO: dispose managed state (managed objects)
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
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