using Hangfire.Common;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nito.AsyncEx.Synchronous;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class StorageConnectionTestsBase : TestBase

{
    private readonly PersistentJobQueueProviderCollection _providers;
    private readonly Mock<IPersistentJobQueue> _queue;

    private readonly ITestOutputHelper _testOutputHelper;

    private EntityFrameworkJobStorage? _storageMock;

    protected StorageConnectionTestsBase(DatabaseFixtureBase fixture,
        ITestOutputHelper testOutputHelper) : base(fixture)
    {
        _testOutputHelper = testOutputHelper;
        _queue = new Mock<IPersistentJobQueue>();

        var provider = new Mock<IPersistentJobQueueProvider>();
        provider.Setup(x => x.GetJobQueue())
            .Returns(_queue.Object);

        _providers = new PersistentJobQueueProviderCollection(provider.Object);
    }

    public override EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions? options = null)
    {
        if (_storageMock == null)
        {
            var storageMock = GetStorageMock(mock =>
                {
                    mock.Setup(x => x.QueueProviders).Returns(_providers);
                    return mock.Object;
                },
                options);

            _storageMock = storageMock;
        }

        return _storageMock;
    }


    /// <summary>
    ///     don't delete this method.  It's needed as a sample method for scheduling
    /// </summary>
    /// <param name="arg"></param>
    public static void SampleMethod(string arg)
    {
    }

    private async Task CreateExpiredJobByCount(int count, bool withNullValue)
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await Task.CompletedTask;
            var createdAt = DateTime.SpecifyKind(new DateTime(2012, 12, 12), DateTimeKind.Utc);
            var items = new Dictionary<string, string>();
            for (var i = 1; i < count + 1; i++) items[$"Key{i}"] = withNullValue ? null : $"Value{i}";

            var jobId = connection.CreateExpiredJob(
                Job.FromExpression(() => SampleMethod("Hello")),
                items,
                createdAt,
                TimeSpan.FromDays(1));

            var jobParameters = dbContext
                .JobParameters.Where(i => i.Job.Id == jobId).ToList();
            var parameters = jobParameters
                .ToDictionary(x => x.Name, x => x.Value);

            Assert.Equal(count, parameters.Count);
            if (withNullValue)
                for (var i = 1; i < count + 1; i++)
                    Assert.Null(parameters[$"Key{i}"]);
            else
                for (var i = 1; i < count + 1; i++)
                    Assert.Equal($"Value{i}", parameters[$"Key{i}"]);
        });
    }

    private void AcquireLock(AcquireLockRequest request)
    {
        UseJobStorageConnection(async connection1 =>
        {
            await Task.CompletedTask;
            lock (request.Mutex)
            {
                _testOutputHelper.WriteLine($"Instance {request.Instance} storage connection opened");
                _testOutputHelper.WriteLine($"Acquiring lock of {request.Seconds} seconds");
            }

            using (connection1.AcquireDistributedLock("exclusive",
                       TimeSpan.FromSeconds(request.Seconds)))
            {
                lock (request.Mutex)
                {
                    _testOutputHelper.WriteLine($"Instance {request.Instance} distributed lock acquired");
                }

                request.InnerAction?.Invoke();
                lock (request.Mutex)
                {
                    _testOutputHelper.WriteLine($"Instance {request.Instance} called inner code");
                }
            }
        }, request.CleanDatabase, request.Options).WaitAndUnwrapException();
    }

    [Fact]
    public async Task AcquireDistributedLock_AcquiresExclusiveApplicationLock_OnSession()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await Task.CompletedTask;
            const string resource = "hello";
            using (connection.AcquireDistributedLock(resource, TimeSpan.FromMinutes(5)))
            {
                var now = DateTime.UtcNow;
                var distributedLock =
                    dbContext.DistributedLocks.SingleOrDefault(i => i.Resource == resource);
                Assert.NotNull(distributedLock);
                Assert.Equal(resource, distributedLock.Resource);
                Assert.InRange(distributedLock.ExpireAt.FromEpochDate().Subtract(now).TotalMinutes, 4, 6);
            }
        });
    }

    [Fact]
    public async Task AcquireDistributedLock_Dispose_ReleasesExclusiveApplicationLock()
    {
        await UseJobStorageConnectionWithDbContext(async (HangfireContext, connection) =>
        {
            await Task.CompletedTask;
            var distributedLock = connection.AcquireDistributedLock("hello", TimeSpan.FromMinutes(5));
            distributedLock.Dispose();

            var tmp = HangfireContext.DistributedLocks.Count(i => i.Resource == "hello");
            Assert.Equal(0, tmp);
        });
    }

    [Fact]
    public async Task AcquireDistributedLock_IsReentrant_FromTheSameConnection_OnTheSameResource()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            using (connection.AcquireDistributedLock("hello", TimeSpan.FromSeconds(15)))
            using (connection.AcquireDistributedLock("hello", TimeSpan.FromSeconds(5)))
            {
                Assert.True(true);
            }
        }, options: new EntityFrameworkStorageOptions { DistributedLockPollInterval = TimeSpan.FromSeconds(1) });
    }

    [Fact]
    public async Task AcquireDistributedLock_ThrowsAnException_IfLockCanNotBeGranted()
    {
        await Task.CompletedTask;
        var mutex = new object();
        var releaseLock = new ManualResetEventSlim(false);
        var lockAcquired = new ManualResetEventSlim(false);

        var options = new EntityFrameworkStorageOptions
        {
            DistributedLockWaitTimeout = TimeSpan.FromSeconds(10),
            DistributedLockPollInterval = TimeSpan.FromSeconds(1)
        };
        var thread = new Thread(
            () =>
            {
                var request = new AcquireLockRequest
                {
                    CleanDatabase = true,
                    InnerAction = () =>
                    {
                        lockAcquired.Set();
                        lock (mutex)
                        {
                            _testOutputHelper.WriteLine($"{nameof(lockAcquired)} set");
                        }

                        releaseLock.Wait();
                        lock (mutex)
                        {
                            _testOutputHelper.WriteLine($"{nameof(releaseLock)} awaited");
                        }
                    },
                    Instance = 1,
                    Mutex = mutex,
                    Seconds = 60,
                    Options = options
                };
                AcquireLock(request);
            });
        thread.Start();
        lock (mutex)
        {
            _testOutputHelper.WriteLine("Internal thread started");
        }

        lockAcquired.Wait();
        lock (mutex)
        {
            _testOutputHelper.WriteLine($"{nameof(lockAcquired)} awaited");
        }

        Assert.Throws<DistributedLockTimeoutException>(
            () =>
            {
                var request = new AcquireLockRequest
                {
                    CleanDatabase = false,
                    Instance = 2,
                    Mutex = mutex,
                    Seconds = 5,
                    Options = options
                };
                AcquireLock(request);
            });
        releaseLock.Set();
        lock (mutex)
        {
            _testOutputHelper.WriteLine($"{nameof(releaseLock)} set");
        }

        thread.Join();
        lock (mutex)
        {
            _testOutputHelper.WriteLine("thread has been joined");
        }
    }

    [Fact]
    public async Task AcquireDistributedLock_ThrowsAnException_WhenResourceIsNullOrEmpty()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => connection.AcquireDistributedLock("", TimeSpan.FromMinutes(5)));

            Assert.Equal("resource", exception.ParamName);
        });
    }

    [Fact]
    public async Task AcquireLock_ReturnsNonNullInstance()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var @lock = connection.AcquireDistributedLock("1", TimeSpan.FromSeconds(1));
            Assert.NotNull(@lock);
        });
    }

    [Fact]
    public async Task AnnounceServer_CreatesOrUpdatesARecord()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await Task.CompletedTask;
            var queues = new[] { "critical", "default" };
            var context1 = new ServerContext
            {
                Queues = queues,
                WorkerCount = 4
            };
            connection.AnnounceServer("server", context1);

            var server = dbContext.Servers.Single();
            Assert.Equal("server", server.Id);
            Assert.NotNull(server.Data);
            var serverData1 = SerializationHelper.Deserialize<ServerData>(server.Data);
            Assert.Equal(4, serverData1.WorkerCount);
            Assert.Equal(queues, serverData1.Queues);
            Assert.NotNull(server.LastHeartbeat);

            var context2 = new ServerContext
            {
                Queues = new[] { "default" },
                WorkerCount = 1000
            };
            connection.AnnounceServer("server", context2);
            dbContext.ChangeTracker.Clear();
            var sameServer = dbContext.Servers.Single();
            Assert.Equal("server", sameServer.Id);
            Assert.NotNull(sameServer.Data);
            var serverData2 = SerializationHelper.Deserialize<ServerData>(sameServer.Data);
            Assert.Equal(1000, serverData2.WorkerCount);
        });
    }

    [Fact]
    public async Task AnnounceServer_ThrowsAnException_WhenContextIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.AnnounceServer("server", null));

            Assert.Equal("context", exception.ParamName);
        });
    }

    [Fact]
    public async Task AnnounceServer_ThrowsAnException_WhenServerIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.AnnounceServer(null, new ServerContext()));

            Assert.Equal("serverId", exception.ParamName);
        });
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateFourParametersWithNonNullValues()
    {
        await CreateExpiredJobByCount(4, false);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateFourParametersWithNullValues()
    {
        await CreateExpiredJobByCount(4, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateJobWithoutParameters()
    {
        await CreateExpiredJobByCount(0, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateManyParametersWithNonNullValues()
    {
        await CreateExpiredJobByCount(5, false);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateManyParametersWithNullValues()
    {
        await CreateExpiredJobByCount(5, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateParametersWithNonNullValues()
    {
        await CreateExpiredJobByCount(1, false);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateParametersWithNullValues()
    {
        await CreateExpiredJobByCount(1, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateThreeParametersWithNonNullValues()
    {
        await CreateExpiredJobByCount(3, false);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateThreeParametersWithNullValues()
    {
        await CreateExpiredJobByCount(3, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateTwoParametersWithNonNullValues()
    {
        await CreateExpiredJobByCount(2, false);
    }

    [Fact]
    public async Task CreateExpiredJob_CanCreateTwoParametersWithNullValues()
    {
        await CreateExpiredJobByCount(2, true);
    }

    [Fact]
    public async Task CreateExpiredJob_CreatesAJobInTheStorage_AndSetsItsParameters()
    {
        const string expectedString = "Hello";
        const string key1 = "Key1";
        const string key2 = "Key2";
        const string expectedValue1 = "Value1";
        const string expectedValue2 = "Value2";
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await Task.CompletedTask;
            var createdAt = new DateTime(2012, 12, 12).ToUniversalTime();
            var dictionary = new Dictionary<string, string> { { key1, expectedValue1 }, { key2, expectedValue2 } };
            var jobId = jobStorage.CreateExpiredJob(
                Job.FromExpression(() => SampleMethod(expectedString)),
                dictionary,
                createdAt,
                TimeSpan.FromDays(1));

            Assert.NotNull(jobId);
            Assert.NotEmpty(jobId);

            var sqlJob = dbContext.Jobs.Single();
            Assert.Equal(jobId, sqlJob.Id);
            Assert.Equal(createdAt, sqlJob.CreatedAt.FromEpochDate());
            Assert.Null(sqlJob.StateName);

            var invocationData = SerializationHelper.Deserialize<InvocationData>(sqlJob.InvocationData);
            invocationData.Arguments = sqlJob.Arguments;

            var job = invocationData.DeserializeJob();
            Assert.Equal(typeof(StorageConnectionTestsBase), job.Type);
            Assert.Equal(nameof(SampleMethod), job.Method.Name);
            Assert.Equal(expectedString, job.Args[0]);

            Assert.True(createdAt.AddDays(1).AddMinutes(-1).ToEpochDate() < sqlJob.ExpireAt);
            Assert.True(sqlJob.ExpireAt < createdAt.AddDays(1).AddMinutes(1).ToEpochDate());

            var parameters = dbContext.JobParameters
                .Where(i => i.Job.Id == jobId)
                .ToDictionary(x => x.Name, x => x.Value);

            Assert.Equal(expectedValue1, parameters[key1]);
            Assert.Equal(expectedValue2, parameters[key2]);
        });
    }

    [Fact]
    public async Task CreateExpiredJob_ThrowsAnException_WhenJobIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.CreateExpiredJob(
                    null,
                    new Dictionary<string, string>(),
                    jobStorage.Storage.UtcNow,
                    TimeSpan.Zero));

            Assert.Equal("job", exception.ParamName);
        });
    }

    [Fact]
    public async Task CreateExpiredJob_ThrowsAnException_WhenParametersCollectionIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.CreateExpiredJob(
                    Job.FromExpression(() => SampleMethod("hello")),
                    null,
                    jobStorage.Storage.UtcNow,
                    TimeSpan.Zero));

            Assert.Equal("parameters", exception.ParamName);
        });
    }

    [Fact]
    public async Task CreateWriteTransaction_ReturnsNonNullInstance()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var transaction = jobStorage.CreateWriteTransaction();
            Assert.NotNull(transaction);
        });
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenStorageIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkJobStorageConnection(null));

        Assert.Equal("storage", exception.ParamName);
    }

    [Fact]
    public async Task FetchNextJob_DelegatesItsExecution_ToTheQueue()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var token = new CancellationToken();
            var queues = new[] { "default" };

            jobStorage.FetchNextJob(queues, token);

            _queue.Verify(x => x.Dequeue(queues, token));
        });
    }

    [Fact]
    public async Task FetchNextJob_Throws_IfMultipleProvidersResolved()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var token = new CancellationToken();
            var anotherProvider = new Mock<IPersistentJobQueueProvider>();
            _providers.Add(anotherProvider.Object, new[] { "critical" });

            Assert.Throws<InvalidOperationException>(
                () => jobStorage.FetchNextJob(new[] { "critical", "default" }, token));
        });
    }

    [Fact]
    public async Task GetAllEntriesFromHash_ReturnsAllKeysAndTheirValues()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            var hashes = new List<_Hash>
            {
                new() { Key = "some-hash", Name = "Key1", Value = "Value1" },
                new() { Key = "some-hash", Name = "Key2", Value = "Value2" },
                new() { Key = "another-hash", Name = "Key3", Value = "Value3" }
            };
            dbContext.AddRange(hashes);

            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetAllEntriesFromHash("some-hash");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Value1", result["Key1"]);
            Assert.Equal("Value2", result["Key2"]);
        });
    }

    [Fact]
    public async Task GetAllEntriesFromHash_ReturnsNull_IfHashDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetAllEntriesFromHash("some-hash");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetAllEntriesFromHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(() => jobStorage.GetAllEntriesFromHash(null));
        });
    }

    [Fact]
    public async Task GetAllItemsFromList_ReturnsAllItems_FromAGivenList()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new _List { Key = "list-1", Value = "1" },
                new _List { Key = "list-2", Value = "2" }, new _List { Key = "list-1", Value = "3" });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetAllItemsFromList("list-1");

            // Assert
            Assert.Equal(new[] { "3", "1" }, result.ToArray());
        });
    }

    [Fact]
    public async Task GetAllItemsFromList_ReturnsAnEmptyList_WhenListDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetAllItemsFromList("my-list");
            Assert.Empty(result);
        });
    }

    [Fact]
    public async Task GetAllItemsFromList_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetAllItemsFromList(null));
        });
    }

    [Fact]
    public async Task GetAllItemsFromSet_ReturnsAllItems()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            var sets = new[]
            {
                new _Set { Key = "some-set", Value = "1" },
                new _Set { Key = "some-set", Value = "2" },
                new _Set { Key = "another-set", Value = "3" }
            };
            foreach (var set in sets) dbContext.Add(set);
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetAllItemsFromSet("some-set");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("1", result);
            Assert.Contains("2", result);
        });
    }

    [Fact]
    public async Task GetAllItemsFromSet_ReturnsEmptyCollection_WhenKeyDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetAllItemsFromSet("some-set");

            Assert.NotNull(result);
            Assert.Empty(result);
        });
    }

    [Fact]
    public async Task GetAllItemsFromSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(() => jobStorage.GetAllItemsFromSet(null));
        });
    }

    [Fact]
    public async Task GetCounter_IncludesValues_FromCounterAggregateTable()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            dbContext.AddRange(new List<_AggregatedCounter>
            {
                new() { Key = "counter-1", Value = 12 },
                new() { Key = "counter-2", Value = 15 }
            });
            await dbContext.SaveChangesAsync();

            //does nothing
            // Act

            var result = jobStorage.GetCounter("counter-1");

            Assert.Equal(12, result);
        });
    }

    [Fact]
    public async Task GetCounter_ReturnsSumOfValues_InCounterTable()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            dbContext.AddRange(new List<_Counter>
            {
                new() { Key = "counter-1", Value = 1 },
                new() { Key = "counter-2", Value = 1 },
                new() { Key = "counter-1", Value = 1 }
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetCounter("counter-1");

            // Assert
            Assert.Equal(2, result);
        });
    }

    [Fact]
    public async Task GetCounter_ReturnsZero_WhenKeyDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetCounter("my-counter");
            Assert.Equal(0, result);
        });
    }


    [Fact]
    public async Task GetCounter_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetCounter(null));
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ReturnsN_WhenMoreThanNExist()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "key", Score = 1.0, Value = "1234" },
                new() { Key = "key", Score = -1.0, Value = "567" },
                new() { Key = "key", Score = -5.0, Value = "890" },
                new() { Key = "another-key", Score = -2.0, Value = "abcd" }
            });
            await dbContext.SaveChangesAsync();


            var result = connection.GetFirstByLowestScoreFromSet("key", -10.0, 10.0, 2);

            Assert.Equal(2, result.Count);
            Assert.Equal("890", result.ElementAt(0));
            Assert.Equal("567", result.ElementAt(1));
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ReturnsN_WhenMoreThanNExist_And_RequestedCountIsGreaterThanN()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "key", Score = 1.0, Value = "1234" },
                new() { Key = "key", Score = -1.0, Value = "567" },
                new() { Key = "key", Score = -5.0, Value = "890" },
                new() { Key = "another-key", Score = -2.0, Value = "abcd" }
            });
            await dbContext.SaveChangesAsync();

            var result = connection.GetFirstByLowestScoreFromSet("another-key", -10.0, 10.0, 5);

            Assert.Single(result);
            Assert.Equal("abcd", result.First());
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ReturnsNull_WhenTheKeyDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetFirstByLowestScoreFromSet(
                "Key", 0, 1);

            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ReturnsTheValueWithTheLowestScore()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "Key", Score = 1, Value = "1.0" },
                new() { Key = "Key", Score = -1, Value = "-1.0" },
                new() { Key = "Key", Score = -5, Value = "-5.0" },
                new() { Key = "another-Key", Score = -2, Value = "-2.0" }
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            var result = jobStorage.GetFirstByLowestScoreFromSet("Key", -1.0, 3.0);

            Assert.Equal("-1.0", result);
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ThrowsAnException_ToScoreIsLowerThanFromScore()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentException>(
                () => jobStorage.GetFirstByLowestScoreFromSet("Key", 0, -1));
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetFirstByLowestScoreFromSet(null, 0, 1));

            Assert.Equal("Key", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        });
    }

    [Fact]
    public async Task GetFirstByLowestScoreFromSet_ThrowsArgException_WhenRequestingLessThanZero()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentException>(
                () => connection.GetFirstByLowestScoreFromSet("key", 0, 1, -1));

            Assert.Equal("count", exception.ParamName);
        });
    }

    [Fact]
    public async Task GetHashCount_ReturnsNumber_OfHashFields()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new List<_Hash>
            {
                new() { Key = "hash-1", Name = "Field-1" },
                new() { Key = "hash-1", Name = "Field-2" },
                new() { Key = "hash-2", Name = "Field-1" }
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetHashCount("hash-1");

            // Assert
            Assert.Equal(2, result);
        });
    }

    [Fact]
    public async Task GetHashCount_ReturnsZero_WhenKeyDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetHashCount("my-hash");
            Assert.Equal(0, result);
        });
    }

    [Fact]
    public async Task GetHashCount_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(() => jobStorage.GetHashCount(null));
        });
    }

    [Fact]
    public async Task GetHashTtl_ReturnsExpirationTimeForHash()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new List<_Hash>
            {
                new() { Key = "hash-1", Name = "Field", ExpireAt = DateTime.UtcNow.AddHours(1).ToEpochDate() },
                new() { Key = "hash-2", Name = "Field", ExpireAt = null }
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetHashTtl("hash-1");

            // Assert
            Assert.True(TimeSpan.FromMinutes(59) < result);
            Assert.True(result < TimeSpan.FromMinutes(61));
        });
    }

    [Fact]
    public async Task GetHashTtl_ReturnsNegativeValue_WhenHashDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetHashTtl("my-hash");
            Assert.True(result < TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task GetHashTtl_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetHashTtl(null));
        });
    }

    [Fact]
    public async Task GetJobData_ReturnsJobLoadException_IfThereWasADeserializationException()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var newJob = new _Job
            {
                InvocationData = SerializationHelper.Serialize(new InvocationData(null, null, null, null)),
                StateName = SucceededState.StateName,
                Arguments = "['Arguments']",
                CreatedAt = DateTime.UtcNow.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing

            var result = jobStorage.GetJobData(newJob.Id);

            Assert.NotNull(result.LoadException);
        });
    }

    [Fact]
    public async Task GetJobData_ReturnsNull_WhenIdentifierCanNotBeParsedAsLong()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var result = connection.GetJobData("some-non-long-id");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetJobData_ReturnsNull_WhenThereIsNoSuchJob()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetJobData("1");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetJobData_ReturnsResult_WhenJobExists()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var job = Job.FromExpression(() => SampleMethod("wrong"));
            var newJob = new _Job
            {
                InvocationData = SerializationHelper.Serialize(InvocationData.SerializeJob(job)),
                StateName = SucceededState.StateName,
                Arguments = "['Arguments']",
                CreatedAt = DateTime.UtcNow.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing
            var jobId = newJob.Id;

            dbContext.ChangeTracker.Clear();
            var result = jobStorage.GetJobData(jobId);

            Assert.NotNull(result);
            Assert.NotNull(result.Job);
            Assert.Equal(SucceededState.StateName, result.State);
            Assert.Equal("Arguments", result.Job.Args[0]);
            Assert.Null(result.LoadException);
            Assert.True(DateTime.UtcNow.AddMinutes(-1) < result.CreatedAt.ToUniversalTime());
            Assert.True(result.CreatedAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(1));
        });
    }

    [Fact]
    public async Task GetJobData_ThrowsAnException_WhenJobIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetJobData(null));
        });
    }

    [Fact]
    public async Task GetListCount_ReturnsTheNumberOfListElements()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new _List { Key = "list-1" }, new _List { Key = "list-1" },
                new _List { Key = "list-2" });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetListCount("list-1");

            // Assert
            Assert.Equal(2, result);
        });
    }

    [Fact]
    public async Task GetListCount_ReturnsZero_WhenListDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetListCount("my-list");
            Assert.Equal(0, result);
        });
    }

    [Fact]
    public async Task GetListCount_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetListCount(null));
        });
    }

    [Fact]
    public async Task GetListTtl_ReturnsExpirationTimeForList()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new _List { Key = "list-1", ExpireAt = DateTime.UtcNow.AddHours(1).ToEpochDate() },
                new _List { Key = "list-2", ExpireAt = null });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetListTtl("list-1");

            // Assert
            Assert.True(TimeSpan.FromMinutes(59) < result);
            Assert.True(result < TimeSpan.FromMinutes(61));
        });
    }

    [Fact]
    public async Task GetListTtl_ReturnsNegativeValue_WhenListDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetListTtl("my-list");
            Assert.True(result < TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task GetListTtl_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetListTtl(null));
        });
    }

    [Fact]
    public async Task GetParameter_ReturnsNull_WhenJobIdCanNotBeParsedAsLong()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var result = connection.GetJobParameter("some-non-long-id", "name");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetParameter_ReturnsNull_WhenParameterDoesNotExists()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var Value = jobStorage.GetJobParameter("1", "hello");
            Assert.Null(Value);
        });
    }

    [Fact]
    public async Task GetParameter_ReturnsParameterValue_WhenJobExists()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var newJob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobParameter { Job = newJob, Name = "name", Value = "Value" });
            await dbContext.SaveChangesAsync();
            //does nothing


            var Value = jobStorage.GetJobParameter(newJob.Id, "name");

            Assert.Equal("Value", Value);
        });
    }

    [Fact]
    public async Task GetParameter_ThrowsAnException_WhenJobIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetJobParameter(null, "hello"));

            Assert.Equal("jobId", exception.ParamName);
        });
    }

    [Fact]
    public async Task GetParameter_ThrowsAnException_WhenNameIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetJobParameter("1", null));

            Assert.Equal("name", exception.ParamName);
        });
    }

    [Fact]
    public async Task GetRangeFromList_ReturnsAllEntries_WithinGivenBounds()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new _List { Key = "list-1", Value = "1" },
                new _List { Key = "list-2", Value = "2" }, new _List { Key = "list-1", Value = "3" },
                new _List { Key = "list-1", Value = "4" }, new _List { Key = "list-1", Value = "5" });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act

            var result = jobStorage.GetRangeFromList("list-1", 1, 2);

            // Assert
            Assert.Equal(new[] { "4", "3" }, result);
        });
    }

    [Fact]
    public async Task GetRangeFromList_ReturnsAnEmptyList_WhenListDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetRangeFromList("my-list", 0, 1);
            Assert.Empty(result);
        });
    }

    [Fact]
    public async Task GetRangeFromList_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetRangeFromList(null, 0, 1));

            Assert.Equal("Key", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        });
    }

    [Fact]
    public async Task GetRangeFromSet_ReturnsPagedElements()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "set-1", Value = "1" },
                new() { Key = "set-1", Value = "2" },
                new() { Key = "set-1", Value = "3" },
                new() { Key = "set-1", Value = "4" },
                new() { Key = "set-2", Value = "4" },
                new() { Key = "set-1", Value = "5" }
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            var result = jobStorage.GetRangeFromSet("set-1", 2, 3);

            Assert.Equal(new[] { "3", "4" }, result);
        });
    }

    [Fact]
    public async Task GetRangeFromSet_ReturnsPagedElements2()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "set-1", Value = "1" },
                new() { Key = "set-1", Value = "2" },
                new() { Key = "set-0", Value = "3" },
                new() { Key = "set-1", Value = "4" },
                new() { Key = "set-2", Value = "1" },
                new() { Key = "set-1", Value = "5" },
                new() { Key = "set-2", Value = "2" },
                new() { Key = "set-1", Value = "3" }
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            var result = jobStorage.GetRangeFromSet("set-1", 0, 4);

            Assert.Equal(new[] { "1", "2", "4", "5", "3" }, result);
        });
    }

    [Fact]
    public async Task GetRangeFromSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(() => jobStorage.GetRangeFromSet(null, 0, 1));
        });
    }

    [Fact]
    public async Task GetSetCount_ReturnsNumberOfElements_InASet()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await dbContext.AddRangeAsync(new List<_Set>
            {
                new() { Key = "set-1", Value = "Value-1" },
                new() { Key = "set-2", Value = "Value-1" },
                new() { Key = "set-1", Value = "Value-2" }
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            var result = jobStorage.GetSetCount("set-1");

            Assert.Equal(2, result);
        });
    }

    [Fact]
    public async Task GetSetCount_ReturnsZero_WhenSetDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetSetCount("my-set");
            Assert.Equal(0, result);
        });
    }

    [Fact]
    public async Task GetSetCount_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetSetCount(null));
        });
    }

    [Fact]
    public async Task GetSetTtl_ReturnsExpirationTime_OfAGivenSet()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await Task.CompletedTask;
            // Arrange
            await dbContext.AddRangeAsync(
                new _Set { Key = "set-1", Value = "1", ExpireAt = DateTime.UtcNow.AddMinutes(60).ToEpochDate() },
                new _Set { Key = "set-2", Value = "2", ExpireAt = null });
            await dbContext.SaveChangesAsync();
            //does nothing

            // Act
            var result = jobStorage.GetSetTtl("set-1");

            // Assert
            Assert.True(TimeSpan.FromMinutes(59) < result);
            Assert.True(result < TimeSpan.FromMinutes(61));
        });
    }

    [Fact]
    public async Task GetSetTtl_ReturnsNegativeValue_WhenSetDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetSetTtl("my-set");
            Assert.True(result < TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task GetSetTtl_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(() => jobStorage.GetSetTtl(null));
        });
    }

    [Fact]
    public async Task GetStateData_ReturnsCorrectData()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var data = new Dictionary<string, string>
            {
                { "Key", "Value" }
            };
            var newJob = new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                StateName = string.Empty,
                CreatedAt = DateTime.UtcNow.ToEpochDate()
            };
            dbContext.Add(newJob);
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _JobState { Job = newJob, Name = "old-state", CreatedAt = DateTime.UtcNow.ToEpochDate() });
            await dbContext.SaveChangesAsync();
            var lastState = new _JobState
            {
                Job = newJob,
                Name = "Name",
                Reason = "Reason",
                CreatedAt = DateTime.UtcNow.ToEpochDate(),
                Data = SerializationHelper.Serialize(data)
            };
            dbContext.Add(lastState);
            await dbContext.SaveChangesAsync();
            //does nothing
            newJob.StateName = lastState.Name;
            newJob.StateReason = lastState.Reason;
            newJob.StateData = lastState.Data;
            newJob.LastStateChangedAt = DateTime.UtcNow.ToEpochDate();
            dbContext.Update(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing


            var jobId = newJob.Id;

            var result = jobStorage.GetStateData(jobId);
            Assert.NotNull(result);

            Assert.Equal("Name", result.Name);
            Assert.Equal("Reason", result.Reason);
            Assert.Equal("Value", result.Data["Key"]);
        });
    }

    [Fact]
    public async Task GetStateData_ReturnsCorrectData_WhenPropertiesAreCamelcased()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var data = new Dictionary<string, string>
            {
                { "Key", "Value" }
            };
            var newJob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobState { Job = newJob, Name = "old-state", CreatedAt = DateTime.UtcNow.ToEpochDate() });
            await dbContext.SaveChangesAsync();
            var jobState = new _JobState
            {
                Job = newJob,
                Name = "Name",
                Reason = "Reason",
                CreatedAt = DateTime.UtcNow.ToEpochDate(),
                Data = SerializationHelper.Serialize(data)
            };
            dbContext.Add(jobState);
            await dbContext.SaveChangesAsync();
            //does nothing
            newJob.StateName = jobState.Name;
            newJob.StateReason = jobState.Reason;
            newJob.StateData = jobState.Data;
            dbContext.Update(newJob);
            await dbContext.SaveChangesAsync();
            //does nothing


            var jobId = newJob.Id;

            var result = jobStorage.GetStateData(jobId);
            Assert.NotNull(result);

            Assert.Equal("Value", result.Data["Key"]);
        });
    }

    [Fact]
    public async Task GetStateData_ReturnsNull_IfThereIsNoSuchState()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetStateData("1");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetStateData_ReturnsNull_WhenIdentifierCanNotBeParsedAsLong()
    {
        await UseJobStorageConnection(async connection =>
        {
            await Task.CompletedTask;
            var result = connection.GetStateData("some-non-long-id");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetStateData_ThrowsAnException_WhenJobIdIsNull()
    {
        await UseJobStorageConnection(
            async jobStorage =>
            {
                await Task.CompletedTask;
                Assert.Throws<ArgumentNullException>(
                    () => jobStorage.GetStateData(null));
            });
    }

    [Fact]
    public async Task GetValueFromHash_ReturnsNull_WhenHashDoesNotExist()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var result = jobStorage.GetValueFromHash("my-hash", "name");
            Assert.Null(result);
        });
    }

    [Fact]
    public async Task GetValueFromHash_ReturnsValue_OfAGivenField()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            // Arrange
            await dbContext.AddRangeAsync(new _Hash { Key = "hash-1", Name = "Field-1", Value = "1" },
                new _Hash { Key = "hash-1", Name = "Field-2", Value = "2" },
                new _Hash { Key = "hash-2", Name = "Field-1", Value = "3" });
            await dbContext.SaveChangesAsync();
            //does nothing

            // Act
            var result = jobStorage.GetValueFromHash("hash-1", "Field-1");

            // Assert
            Assert.Equal("1", result);
        });
    }

    [Fact]
    public async Task GetValueFromHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetValueFromHash(null, "name"));

            Assert.Equal("Key", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        });
    }

    [Fact]
    public async Task GetValueFromHash_ThrowsAnException_WhenNameIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.GetValueFromHash("Key", null));

            Assert.Equal("name", exception.ParamName);
        });
    }

    [Fact]
    public async Task Heartbeat_ThrowsAnException_WhenServerIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.Heartbeat(null));
        });
    }

    [Fact]
    public async Task Heartbeat_UpdatesLastHeartbeat_OfTheServerWithGivenId()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            dbContext.Add(new _Server
            {
                Id = "server1",
                Data = string.Empty,
                LastHeartbeat = new DateTime(2012, 12, 12, 12, 12, 12).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Server
            {
                Id = "server2",
                Data = string.Empty,
                LastHeartbeat = new DateTime(2012, 12, 12, 12, 12, 12).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            jobStorage.Heartbeat("server1");
            dbContext.ChangeTracker.Clear();
            var servers = dbContext.Servers
                .ToDictionary(x => x.Id, x => x.LastHeartbeat.FromEpochDate().Value);

            Assert.NotEqual(2012, servers["server1"].Year);
            Assert.Equal(2012, servers["server2"].Year);
        });
    }

    [Fact]
    public async Task RemoveServer_RemovesAServerRecord()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            dbContext.Add(new _Server { Id = "Server1", Data = string.Empty, LastHeartbeat = DateTime.UtcNow.ToEpochDate() });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Server { Id = "Server2", Data = string.Empty, LastHeartbeat = DateTime.UtcNow.ToEpochDate() });
            await dbContext.SaveChangesAsync();
            //does nothing

            jobStorage.RemoveServer("Server1");

            var server = dbContext.Servers.Single();
            Assert.NotEqual("Server1", server.Id, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task RemoveServer_ThrowsAnException_WhenServerIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentNullException>(
                () => jobStorage.RemoveServer(null));
        });
    }

    [Fact]
    public async Task RemoveTimedOutServers_DoItsWorkPerfectly()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await dbContext.AddRangeAsync(new _Server
            {
                Id = "server1",
                LastHeartbeat = DateTime.UtcNow.AddDays(-1).ToEpochDate(),
                Data = string.Empty
            }, new _Server
            {
                Id = "server2",
                LastHeartbeat = DateTime.UtcNow.AddHours(-12).ToEpochDate(),
                Data = string.Empty
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            jobStorage.RemoveTimedOutServers(TimeSpan.FromHours(15));

            var liveServer = dbContext.Servers.Single();
            Assert.Equal("server2", liveServer.Id);
        });
    }

    [Fact]
    public async Task RemoveTimedOutServers_ThrowsAnException_WhenTimeOutIsNegative()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            Assert.Throws<ArgumentException>(
                () => jobStorage.RemoveTimedOutServers(TimeSpan.FromMinutes(-5)));
        });
    }

    [Fact]
    public async Task SetParameter_CanAcceptNulls_AsValues()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var newJob = await InsertNewJob(dbContext);
            var jobId = newJob.Id;
            //does nothing

            jobStorage.SetJobParameter(jobId, "Name", null);

            var parameter = dbContext.JobParameters.Single(i => i.Job == newJob && i.Name == "Name");

            Assert.Null(parameter.Value);
        });
    }

    [Fact]
    public async Task SetParameter_ThrowsAnException_WhenJobIdIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.SetJobParameter(null, "name", "Value"));

            Assert.Equal("jobId", exception.ParamName);
        });
    }

    [Fact]
    public async Task SetParameter_ThrowsAnException_WhenNameIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.SetJobParameter("1", null, "Value"));

            Assert.Equal("name", exception.ParamName);
        });
    }

    [Fact]
    public async Task SetParameter_UpdatesValue_WhenParameterWithTheGivenName_AlreadyExists()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var newJob = await InsertNewJob(dbContext);
            var jobId = newJob.Id;

            jobStorage.SetJobParameter(jobId, "Name", "Value");
            jobStorage.SetJobParameter(jobId, "Name", "AnotherValue");
            //does nothing

            var parameter = dbContext.JobParameters.Single(i => i.Job == newJob && i.Name == "Name");

            Assert.Equal("AnotherValue", parameter.Value);
        });
    }

    [Fact]
    public async Task SetParameters_CreatesNewParameter_WhenParameterWithTheGivenNameDoesNotExists()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            var newJob = await InsertNewJob(dbContext);

            var jobId = newJob.Id;

            jobStorage.SetJobParameter(jobId, "Name", "Value");
            //does nothing

            var parameter = dbContext.JobParameters.Single(i => i.Job == newJob && i.Name == "Name");

            Assert.Equal("Value", parameter.Value);
        });
    }

    [Fact]
    public async Task SetRangeInHash_CanCreateFieldsWithNullValues()
    {
        await UseJobStorageConnectionWithDbContext(async (sql, connection) =>
        {
            await Task.CompletedTask;
            connection.SetRangeInHash("some-hash", new Dictionary<string, string>
            {
                { "Key1", (string)null }
            });

            var result = sql.Hashes.Where(i => i.Key == "some-hash")
                .ToDictionary(x => x.Name, x => x.Value);

            Assert.Null(result["Key1"]);
        });
    }

    [Fact]
    public async Task SetRangeInHash_MergesAllRecords()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, jobStorage) =>
        {
            await Task.CompletedTask;
            jobStorage.SetRangeInHash("some-hash", new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            });
            //does nothing

            var result = dbContext.Hashes.Where(i => i.Key == "some-hash")
                .ToDictionary(x => x.Name, x => x.Value);

            Assert.Equal("Value1", result["Key1"]);
            Assert.Equal("Value2", result["Key2"]);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ReleasesTheAcquiredLock()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            connection.SetRangeInHash("some-hash", new Dictionary<string, string>
            {
                { "Key", "Value" }
            });

            var result = await dbContext.DistributedLocks.Where(i =>
                i.Resource == EntityFrameworkJobStorageConnection.HashDistributedLockName).ToListAsync();
            Assert.Empty(result);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.SetRangeInHash(null, new Dictionary<string, string>()));

            Assert.Equal("Key", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ThrowsAnException_WhenKeyValuePairsArgumentIsNull()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => jobStorage.SetRangeInHash("some-hash", null));

            Assert.Equal("keyValuePairs", exception.ParamName);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ThrowsSqlException_WhenKeyIsTooLong()
    {
        await UseJobStorageConnection(async jobStorage =>
        {
            await Task.CompletedTask;
            try
            {
                var key = new string('a', 9999);
                jobStorage.SetRangeInHash(key,
                    new Dictionary<string, string> { { "field", "value" } });
                var count = jobStorage.GetHashCount(key);
                Assert.Equal(1, count);
            }
            catch (Exception m)
            {
                Assert.NotNull(m);
            }
        });
    }

    private class AcquireLockRequest
    {
        public bool CleanDatabase { get; set; }
        public int Seconds { get; set; }
        public int Instance { get; set; }
        public Action? InnerAction { get; set; }
        public object? Mutex { get; set; }
        public EntityFrameworkStorageOptions? Options { get; set; }
    }
}