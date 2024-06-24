using System;
using System.Transactions;
using Hangfire.EntityFrameworkStorage.Maps;

namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkStorageOptions
{
    internal const string DefaultTablePrefix = "Hangfire_";
    private TimeSpan _countersAggregateInterval;
    private int? _dashboardJobListLimit;
    private TimeSpan _dbmsTimeSyncInterval;
    private TimeSpan _deadlockRetryInterval;
    private TimeSpan _distributedLockPollInterval;

    private TimeSpan _distributedLockTimeout = TimeSpan.FromSeconds(50);
    private TimeSpan _invisibilityTimeout;

    private TimeSpan _jobExpirationCheckInterval;
    private TimeSpan _jobQueueDistributedLockTimeout;
    private TimeSpan _queuePollInterval;


    private TimeSpan _transactionTimeout;

    public EntityFrameworkStorageOptions()
    {
        UpdateSchema = true;
        TransactionIsolationLevel = IsolationLevel.Serializable;
        QueuePollInterval = TimeSpan.FromSeconds(15);
        JobExpirationCheckInterval = TimeSpan.FromMinutes(15);
        CountersAggregateInterval = TimeSpan.FromMinutes(5);
        UpdateSchema = true;
        DashboardJobListLimit = 50000;
        TransactionTimeout = TimeSpan.FromMinutes(1);
        InvisibilityTimeout = TimeSpan.FromMinutes(15);
        JobQueueDistributedLockTimeout = TimeSpan.FromMinutes(1);
        DistributedLockPollInterval = TimeSpan.FromSeconds(Defaults.DistributedLockPollIntervalSeconds);
        DeadlockRetryInterval = TimeSpan.FromSeconds(1);
#pragma warning disable 618
        DbmsTimeSyncInterval = TimeSpan.FromMinutes(5);
#pragma warning restore 618
        TablePrefix = DefaultTablePrefix;
    }


    /// <summary>
    ///     Database schema into which tables will be created. Default is database provider specific ("dbo" for SQL Server,
    ///     "public" for PostgreSQL, etc).
    /// </summary>
    public string DefaultSchema { get; set; }

    /// <summary>
    ///     if set to true, then this provider creates database tables. Default is true
    /// </summary>
    public bool UpdateSchema { get; set; }


    public bool LogSqlInConsole { get; set; }
    public bool LogFormattedSql { get; set; }
    public int DeleteExpiredBatchSize { get; set; } = 1000;

    /// <summary>
    ///     During a distributed lock acquisition, determines how often will Hangfire poll against the database while it waits.
    ///     Must be a positive timespan.
    /// </summary>
    public TimeSpan DistributedLockPollInterval
    {
        get => _distributedLockPollInterval;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(DistributedLockPollInterval));
            _distributedLockPollInterval = value;
        }
    }

    /// <summary>
    ///     When the database encounters a deadlock state, how long to wait before retrying.  Must be a positive timespan.
    /// </summary>
    public TimeSpan DeadlockRetryInterval
    {
        get => _deadlockRetryInterval;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(DeadlockRetryInterval));
            _deadlockRetryInterval = value;
        }
    }

    /// <summary>
    ///     How long to wait to get a distributed lock before throwing an exception.  Must be a positive timespan.
    /// </summary>
    public TimeSpan DistributedLockWaitTimeout
    {
        get => _distributedLockTimeout;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(DistributedLockWaitTimeout));
            _distributedLockTimeout = value;
        }
    }

    /// <summary>
    ///     How long to wait to get a distributed lock for the job queue.  Must be a positive timespan.
    /// </summary>
    public TimeSpan JobQueueDistributedLockTimeout
    {
        get => _jobQueueDistributedLockTimeout;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(JobQueueDistributedLockTimeout));
            _jobQueueDistributedLockTimeout = value;
        }
    }

    /// <summary>
    ///     How long a job can run before Hangfire tries to re-queue it.  Must be a positive timespan.
    /// </summary>
    public TimeSpan InvisibilityTimeout
    {
        get => _invisibilityTimeout;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(InvisibilityTimeout));
            _invisibilityTimeout = value;
        }
    }

    /// <summary>
    ///     For database operations specific to this provider, determines what level of access other transactions have to
    ///     volatile data before a transaction completes.
    /// </summary>
    public IsolationLevel TransactionIsolationLevel { get; set; }

    /// <summary>
    ///     How often this provider will check for new jobs and kick them off.  Must be a positive timespan.
    /// </summary>
    public TimeSpan QueuePollInterval
    {
        get => _queuePollInterval;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(QueuePollInterval));
            _queuePollInterval = value;
        }
    }

    /// <summary>
    ///     Create the schema if it doesn't already exist.
    /// </summary>
    [Obsolete("Use property " + nameof(UpdateSchema) + ".")]
    public bool PrepareSchemaIfNecessary
    {
        get => UpdateSchema;
        set => UpdateSchema = value;
    }

    /// <summary>
    ///     How often this library invokes your DBMS's GetDate (or similar) function for the purpose of inserting timestamps
    ///     into the database.  Because of the ORM
    ///     approach, table insertions can't invoke server-side date functions directly.  Must be a positive timespan.
    /// </summary>
    public TimeSpan DbmsTimeSyncInterval
    {
        get => _dbmsTimeSyncInterval;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(DbmsTimeSyncInterval));
            _dbmsTimeSyncInterval = value;
        }
    }

    /// <summary>
    ///     How often this provider will check for expired jobs and delete them from the database.  Must be a positive
    ///     timespan.
    /// </summary>
    public TimeSpan JobExpirationCheckInterval
    {
        get => _jobExpirationCheckInterval;
        set
        {
            if (value.Ticks < 0)
                throw new ArgumentException($"{nameof(JobExpirationCheckInterval)} should not be negative");
            _jobExpirationCheckInterval = value;
        }
    }

    /// <summary>
    ///     How often this provider will aggregate the job data to display it in the user interface.  This aggregation saves on
    ///     table space and generally improves performance of the UI.  Must be a positive timespan.
    /// </summary>
    public TimeSpan CountersAggregateInterval
    {
        get => _countersAggregateInterval;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(CountersAggregateInterval));
            _countersAggregateInterval = value;
        }
    }

    /// <summary>
    ///     The maximum number of jobs to show in the Hangfire dashboard.  Use null to show all jobs, or a positive integer.
    /// </summary>
    public int? DashboardJobListLimit
    {
        get => _dashboardJobListLimit;
        set
        {
            if (value != null)
                ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(DashboardJobListLimit));
            _dashboardJobListLimit = value;
        }
    }

    /// <summary>
    ///     The maximum time span of transactions containing internal database operation for this provider.  Must be a positive
    ///     integer.
    /// </summary>
    public TimeSpan TransactionTimeout
    {
        get => _transactionTimeout;
        set
        {
            ArgumentHelper.ThrowIfValueIsNotPositive(value, nameof(TransactionTimeout));
            _transactionTimeout = value;
        }
    }

    public string TablePrefix { get; set; } = "Hangfire_";
}