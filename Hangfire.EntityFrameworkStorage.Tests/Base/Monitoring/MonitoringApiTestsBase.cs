using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;

public abstract class MonitoringApiTestsBase : TestBase
{
    private static readonly string InvocationData = SerializationHelper.Serialize(
        new InvocationData(typeof(Console).AssemblyQualifiedName, nameof(Console.WriteLine),
            SerializationHelper.Serialize(new[] { typeof(string).AssemblyQualifiedName }),
            SerializationHelper.Serialize(new[] { "test" })));

    private readonly EntityFrameworkMonitoringApi _api;

    private readonly string _arguments = "[\"test\"]";

    private readonly DateTime _createdAt;
    private readonly DateTime _expireAt;


    private readonly EntityFrameworkJobStorage _storage;

    private EntityFrameworkJobStorage? _storageMock;

    protected MonitoringApiTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
        _storage = GetStorage();
        _api = new EntityFrameworkMonitoringApi(_storage);
        _createdAt = _storage.UtcNow;
        _expireAt = _storage.UtcNow.AddMinutes(1);
    }

    public override EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions? options = null)
    {
        if (_storageMock == null)
        {
            var persistentJobQueueMonitoringApiMock = new Mock<IPersistentJobQueueMonitoringApi>();
            persistentJobQueueMonitoringApiMock.Setup(m => m.GetQueues()).Returns(new[] { "default" });

            var defaultProviderMock = new Mock<IPersistentJobQueueProvider>();
            defaultProviderMock.Setup(m => m.GetJobQueueMonitoringApi())
                .Returns(persistentJobQueueMonitoringApiMock.Object);
            _storageMock = GetStorageMock(mock =>
            {
                mock
                    .Setup(m => m.QueueProviders)
                    .Returns(new PersistentJobQueueProviderCollection(defaultProviderMock.Object));
                return mock.Object;
            }, options);
        }

        return _storageMock;
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnDeletedCount()
    {
        const int expectedStatsDeletedCount = 7;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            dbContext.Add(new _AggregatedCounter { Key = "stats:deleted", Value = 5 });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Counter { Key = "stats:deleted", Value = 1 });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Counter { Key = "stats:deleted", Value = 1 });
            await dbContext.SaveChangesAsync();

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedStatsDeletedCount, result.Deleted);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnEnqueuedCount()
    {
        const int expectedEnqueuedCount = 1;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            dbContext.Add(new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                StateName = Hangfire.States.EnqueuedState.StateName,
                CreatedAt = DateTime.UtcNow.ToEpochDate()
            });
            await dbContext.SaveChangesAsync();

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedEnqueuedCount, result.Enqueued);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnFailedCount()
    {
        const int expectedFailedCount = 2;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            for (var i = 0; i < 2; i++)
            {
                dbContext.Add(new _Job
                {
                    InvocationData = string.Empty,
                    Arguments = string.Empty,
                    CreatedAt = DateTime.UtcNow.ToEpochDate(),
                    StateName = Hangfire.States.FailedState.StateName
                });
                await dbContext.SaveChangesAsync();
            }

            //does nothing

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedFailedCount, result.Failed);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnProcessingCount()
    {
        const int expectedProcessingCount = 1;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            dbContext.Add(new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                CreatedAt = _storage.UtcNow.ToEpochDate(),
                StateName = Hangfire.States.ProcessingState.StateName
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedProcessingCount, result.Processing);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnQueuesCount()
    {
        const int expectedQueuesCount = 1;
        var _sut = new EntityFrameworkMonitoringApi(_storage);
        var result = _sut.GetStatistics();

        Assert.Equal(expectedQueuesCount, result.Queues);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnRecurringCount()
    {
        const int expectedRecurringCount = 1;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            dbContext.Add(new _Set { Key = "recurring-jobs", Value = "test", Score = 0 });
            await dbContext.SaveChangesAsync();

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedRecurringCount, result.Recurring);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnScheduledCount()
    {
        const int expectedScheduledCount = 3;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            for (var i = 0; i < 3; i++)
            {
                dbContext.Add(new _Job
                {
                    InvocationData = string.Empty,
                    CreatedAt = DateTime.UtcNow.ToEpochDate(),
                    Arguments = string.Empty,
                    StateName = Hangfire.States.ScheduledState.StateName
                });
                await dbContext.SaveChangesAsync();
            }

            //does nothing
            await dbContext.SaveChangesAsync();

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedScheduledCount, result.Scheduled);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnServersCount()
    {
        const int expectedServersCount = 2;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            for (var i = 1; i < 3; i++)
            {
                dbContext.Add(new _Server { Id = i.ToString(), Data = i.ToString() });
                await dbContext.SaveChangesAsync();
            }

            //does nothing

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedServersCount, result.Servers);
    }

    [Fact]
    public async Task GetStatistics_ShouldReturnSucceededCount()
    {
        const int expectedStatsSucceededCount = 11;

        StatisticsDto? result = null;
        await UseDbContext(async (dbContext) =>
        {
            dbContext.Add(new _Counter { Key = "stats:succeeded", Value = 1 });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _AggregatedCounter { Key = "stats:succeeded", Value = 10 });
            await dbContext.SaveChangesAsync();
            //does nothing

            result = _api.GetStatistics();
        });

        Assert.Equal(expectedStatsSucceededCount, result.Succeeded);
    }

    [Fact]
    public async Task JobDetails_ShouldReturnCreatedAtAndExpireAt()
    {
        JobDetailsDto? result = null;

        await UseDbContext(async (dbContext) =>
        {
            var newJob = new _Job
            {
                CreatedAt = _createdAt.ToEpochDate(),
                InvocationData = InvocationData,
                Arguments = _arguments,
                ExpireAt = _expireAt.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing
            var jobId = newJob.Id;

            result = _api.JobDetails(jobId);
        });

        Assert.InRange(result.CreatedAt.Value.Subtract(_createdAt).TotalSeconds, -3, 60);
        Assert.InRange(result.ExpireAt.Value.Subtract(_expireAt).TotalSeconds, -3, 60);
    }

    [Fact]
    public async Task JobDetails_ShouldReturnHistory()
    {
        const string stateData =
            "{\"EnqueueAt\":\"2016-02-21T11:56:05.0561988Z\", \"ScheduledAt\":\"2016-02-21T11:55:50.0561988Z\"}";

        JobDetailsDto? result = null;

        await UseDbContext(async (dbContext) =>
        {
            var newJob = new _Job
            {
                CreatedAt = _createdAt.ToEpochDate(),
                InvocationData = InvocationData,
                Arguments = _arguments,
                ExpireAt = _expireAt.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _JobState
            {
                Job = newJob,
                CreatedAt = _createdAt.ToEpochDate(),
                Name = Hangfire.States.ScheduledState.StateName,
                Data = stateData
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            var jobId = newJob.Id;
            dbContext.ChangeTracker.Clear();
            result = _api.JobDetails(jobId);
        });

        Assert.Single(result.History);
    }

    [Fact]
    public async Task JobDetails_ShouldReturnJob()
    {
        JobDetailsDto? result = null;

        await UseDbContext(async (dbContext) =>
        {
            var newJob = new _Job
            {
                CreatedAt = _createdAt.ToEpochDate(),
                InvocationData = InvocationData,
                Arguments = _arguments,
                ExpireAt = _expireAt.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing
            var jobId = newJob.Id;


            result = _api.JobDetails(jobId);
        });

        Assert.NotNull(result.Job);
    }

    [Fact]
    public async Task JobDetails_ShouldReturnProperties()
    {
        var properties = new Dictionary<string, string>
        {
            ["CurrentUICulture"] = "en-US",
            ["CurrentCulture"] = "lt-LT"
        };

        JobDetailsDto? result = null;

        await UseDbContext(async (dbContext) =>
        {
            var newJob = new _Job
            {
                CreatedAt = _createdAt.ToEpochDate(),
                InvocationData = InvocationData,
                Arguments = _arguments,
                ExpireAt = _expireAt.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();

            foreach (var x in properties)
            {
                dbContext.Add(new _JobParameter { Job = newJob, Name = x.Key, Value = x.Value });
                await dbContext.SaveChangesAsync();
            }

            //does nothing
            var jobId = newJob.Id;

            result = _api.JobDetails(jobId);
        });

        Assert.Equal(properties, result?.Properties);
    }
}