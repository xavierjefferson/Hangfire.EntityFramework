using System.Data.Entity;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Hangfire.States;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class WriteOnlyTransactionTestsBase : TestBase
{
    protected WriteOnlyTransactionTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
        var defaultProvider = new Mock<IPersistentJobQueueProvider>();
        defaultProvider.Setup(x => x.GetJobQueue())
            .Returns(new Mock<IPersistentJobQueue>().Object);
    }

    private static async Task<InsertTwoJobsResult> InsertTwoJobs(HangfireContext dbContext, Action<_Job>? action = null)
    {
        var insertTwoJobsResult = new InsertTwoJobsResult();


        for (var i = 0; i < 2; i++)
        {
            var newJob = await InsertNewJob(dbContext, action);

            if (i == 0)
                insertTwoJobsResult.JobId1 = newJob.Id;
            else
                insertTwoJobsResult.JobId2 = newJob.Id;
        }

        return insertTwoJobsResult;
    }

    private static async Task<_Job> GetTestJob(HangfireContext dbContext, string? jobId)
    {
        await Task.CompletedTask;
        return dbContext.Jobs.Single(i => i.Id == jobId);
    }

    private void Commit(
        HangfireContext connection,
        Action<EntityFrameworkWriteOnlyTransaction> action)
    {
        using (var transaction = new EntityFrameworkWriteOnlyTransaction(GetStorage()))
        {
            action(transaction);
            transaction.Commit();
        }
    }

    [Fact]
    public async Task AddJobState_JustAddsANewRecordInATable()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            //Arrange
            var newJob = await InsertNewJob(dbContext);

            var jobId = newJob.Id;

            var state = new Mock<IState>();
            state.Setup(x => x.Name).Returns("State");
            state.Setup(x => x.Reason).Returns("Reason");
            state.Setup(x => x.SerializeData())
                .Returns(new Dictionary<string, string> { { "Name", "Value" } });

            Commit(dbContext, x => x.AddJobState(jobId, state.Object));

            var job = await GetTestJob(dbContext, jobId);
            Assert.Null(job.StateName);


            var jobState = dbContext.JobStates.Single();

            Assert.Equal(jobId, jobState.Job.Id);
            Assert.Equal("State", jobState.Name);
            Assert.Equal("Reason", jobState.Reason);
            Assert.InRange(connection.Storage.UtcNow.Subtract(jobState.CreatedAt.FromEpochDate()).TotalSeconds, -3, 10);
            Assert.Equal("{\"Name\":\"Value\"}", jobState.Data);
        });
    }

    [Fact]
    public async Task AddRangeToSet_AddsAllItems_ToAGivenSet()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var items = new List<string> { "1", "2", "3" };

            Commit(dbContext, x => x.AddRangeToSet("my-set", items));
            dbContext.ChangeTracker.Clear();
            var records = dbContext.Sets.Where(i => i.Key == "my-set").OrderBy(i => i.Id).Select(i => i.Value).ToList();
            Assert.Equal(items, records);
        });
    }

    [Fact]
    public async Task AddRangeToSet_ThrowsAnException_WhenItemsValueIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.AddRangeToSet("my-set", null)));

            Assert.Equal("items", exception.ParamName);
        });
    }

    [Fact]
    public async Task AddRangeToSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.AddRangeToSet(null, new List<string>())));

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task AddToQueue_CallsEnqueue_OnTargetPersistentQueue()
    {
        var correctJobQueue = new Mock<IPersistentJobQueue>();
        var correctProvider = new Mock<IPersistentJobQueueProvider>();
        correctProvider.Setup(x => x.GetJobQueue())
            .Returns(correctJobQueue.Object);


        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            connection.Storage.QueueProviders.Add(correctProvider.Object, new[] { "default" });
            var job = await InsertNewJob(dbContext);
            Commit(dbContext, x => x.AddToQueue("default", job.Id));

            correctJobQueue.Verify(x =>
                x.Enqueue(It.IsNotNull<HangfireContext>(), "default", job.Id));
        });
    }

    [Fact]
    public async Task AddToSet_AddsARecord_IfThereIsNo_SuchKeyAndValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x => x.AddToSet("my-key", "my-value"));

            var record = dbContext.Sets.Single();

            Assert.Equal("my-key", record.Key);
            Assert.Equal("my-value", record.Value);
            Assert.Equal(0.0, record.Score, 2);
        });
    }

    [Fact]
    public async Task AddToSet_AddsARecord_WhenKeyIsExists_ButValuesAreDifferent()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.AddToSet("my-key", "another-value");
            });

            var recordCount = dbContext.Sets.Count();

            Assert.Equal(2, recordCount);
        });
    }

    [Fact]
    public async Task AddToSet_DoesNotAddARecord_WhenBothKeyAndValueAreExist()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.AddToSet("my-key", "my-value");
            });

            Assert.Single(dbContext.Sets);
        });
    }

    [Fact]
    public async Task AddToSet_WithScore_AddsARecordWithScore_WhenBothKeyAndValueAreNotExist()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x => x.AddToSet("my-key", "my-value", 3.2));

            var record = dbContext.Sets.Single();

            Assert.Equal("my-key", record.Key);
            Assert.Equal("my-value", record.Value);
            Assert.Equal(3.2, record.Score, 3);
        });
    }

    [Fact]
    public async Task AddToSet_WithScore_UpdatesAScore_WhenBothKeyAndValueAreExist()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.AddToSet("my-key", "my-value", 3.2);
            });

            var record = dbContext.Sets.Single();

            Assert.Equal(3.2, record.Score, 3);
        });
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_IfStorageIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkWriteOnlyTransaction(null));

        Assert.Equal("storage", exception.ParamName);
    }

    [Fact]
    public async Task DecrementCounter_AddsRecordToCounterTable_WithNegativeValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x => x.DecrementCounter("my-key"));

            var record = dbContext.Counters.Single();

            Assert.Equal("my-key", record.Key);
            Assert.Equal(-1, record.Value);
            Assert.Null(record.ExpireAt);
        });
    }

    [Fact]
    public async Task DecrementCounter_WithExistingKey_AddsAnotherRecord()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x =>
            {
                x.DecrementCounter("my-key");
                x.DecrementCounter("my-key");
            });


            var recordCount = dbContext.Counters.Count();

            Assert.Equal(2, recordCount);
        });
    }

    [Fact]
    public async Task DecrementCounter_WithExpiry_AddsARecord_WithExpirationTimeSet()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            Commit(dbContext, x => x.DecrementCounter("my-key", TimeSpan.FromDays(1)));

            var record = dbContext.Counters.Single();

            Assert.Equal("my-key", record.Key);
            Assert.Equal(-1, record.Value);
            Assert.NotNull(record.ExpireAt);

            var expireAt = record.ExpireAt.FromEpochDate();

            Assert.True(DateTime.UtcNow.AddHours(23) < expireAt);
            Assert.True(expireAt < DateTime.UtcNow.AddHours(25));
        });
    }

    [Fact]
    public async Task ExpireHash_SetsExpirationTimeOnAHash_WithGivenKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _Hash { Key = "hash-1", Name = "field" });
            dbContext.Add(new _Hash { Key = "hash-2", Name = "field" });
            await dbContext.SaveChangesAsync();
            //does nothing

            // Act
            Commit(dbContext, x => x.ExpireHash("hash-1", TimeSpan.FromMinutes(60)));

            // Assert
            dbContext.ChangeTracker.Clear();
            var records = dbContext.Hashes
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.True(DateTime.UtcNow.AddMinutes(59).ToEpochDate() < records["hash-1"]);
            Assert.True(records["hash-1"] < DateTime.UtcNow.AddMinutes(61).ToEpochDate());
            Assert.Null(records["hash-2"]);
        });
    }

    [Fact]
    public async Task ExpireHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.ExpireHash(null, TimeSpan.FromMinutes(5))));

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task ExpireJob_SetsJobExpirationData()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            var insertTwoResult = await InsertTwoJobs(dbContext);

            Commit(dbContext, x => x.ExpireJob(insertTwoResult.JobId1, TimeSpan.FromDays(1)));
            //Act
            dbContext.ChangeTracker.Clear();
            var job = await GetTestJob(dbContext, insertTwoResult.JobId1);
            //Assert

            Assert.True(DateTime.UtcNow.AddMinutes(-1).ToEpochDate() < job.ExpireAt &&
                        job.ExpireAt <= DateTime.UtcNow.AddDays(1).ToEpochDate());

            var anotherJob = await GetTestJob(dbContext, insertTwoResult.JobId2);
            Assert.Null(anotherJob.ExpireAt);
        });
    }

    [Fact]
    public async Task ExpireList_SetsExpirationTime_OnAList_WithGivenKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _List { Key = "list-1", Value = "1" });
            dbContext.Add(new _List { Key = "list-2", Value = "1" });
            await dbContext.SaveChangesAsync();
            //does nothing

            // Act
            Commit(dbContext, x => x.ExpireList("list-1", TimeSpan.FromMinutes(60)));
            dbContext.ChangeTracker.Clear();
            // Assert

            var records = dbContext.Lists
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.True(DateTime.UtcNow.AddMinutes(59).ToEpochDate() < records["list-1"]);
            Assert.True(records["list-1"] < DateTime.UtcNow.AddMinutes(61).ToEpochDate());
            Assert.Null(records["list-2"]);
        });
    }

    [Fact]
    public async Task ExpireList_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.ExpireList(null, TimeSpan.FromSeconds(45))));

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task ExpireSet_SetsExpirationTime_OnASet_WithGivenKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _Set { Key = "set-1", Value = "1" });
            dbContext.Add(new _Set { Key = "set-2", Value = "1" });
            await dbContext.SaveChangesAsync();
            //does nothing

            // Act
            Commit(dbContext, x => x.ExpireSet("set-1", TimeSpan.FromMinutes(60)));
            dbContext.ChangeTracker.Clear();
            // Assert

            var records = dbContext.Sets
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.True(DateTime.UtcNow.AddMinutes(59).ToEpochDate() < records["set-1"]);
            Assert.True(records["set-1"] < DateTime.UtcNow.AddMinutes(61).ToEpochDate());
            Assert.Null(records["set-2"]);
        });
    }

    [Fact]
    public async Task ExpireSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.ExpireSet(null, TimeSpan.FromSeconds(45))));

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task IncrementCounter_AddsRecordToCounterTable_WithPositiveValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x => x.IncrementCounter("my-key"));
            //Act
            var record = dbContext.Counters.Single();
            //Assert
            Assert.Equal("my-key", record.Key);
            Assert.Equal(1, record.Value);
            Assert.Null(record.ExpireAt);
        });
    }

    [Fact]
    public async Task IncrementCounter_WithExistingKey_AddsAnotherRecord()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.IncrementCounter("my-key");
                x.IncrementCounter("my-key");
            });
            //Act
            var recordCount = dbContext.Counters.Count();
            //Assert

            Assert.Equal(2, recordCount);
        });
    }

    [Fact]
    public async Task IncrementCounter_WithExpiry_AddsARecord_WithExpirationTimeSet()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x => x.IncrementCounter("my-key", TimeSpan.FromDays(1)));
            //Act
            var record = dbContext.Counters.Single();
            //Assert
            Assert.Equal("my-key", record.Key);
            Assert.Equal(1, record.Value);
            Assert.NotNull(record.ExpireAt);

            var expireAt = record.ExpireAt.FromEpochDate();

            Assert.True(DateTime.UtcNow.AddHours(23) < expireAt);
            Assert.True(expireAt < DateTime.UtcNow.AddHours(25));
        });
    }

    [Fact]
    public async Task InsertToList_AddsAnotherRecord_WhenBothKeyAndValueAreExist()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "my-value");
                x.InsertToList("my-key", "my-value");
            });
            //Act
            var recordCount = dbContext.Lists.Count();
            //Assert
            Assert.Equal(2, recordCount);
        });
    }

    [Fact]
    public async Task InsertToList_AddsARecord_WithGivenValues()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x => x.InsertToList("my-key", "my-value"));
            //Act
            var record = dbContext.Lists.Single();

            //Assert
            Assert.Equal("my-key", record.Key);
            Assert.Equal("my-value", record.Value);
        });
    }

    [Fact]
    public async Task PersistHash_ClearsExpirationTime_OnAGivenHash()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _Hash { Key = "hash-1", Name = "field", ExpireAt = DateTime.UtcNow.AddDays(1).ToEpochDate() });
            dbContext.Add(new _Hash { Key = "hash-2", Name = "field", ExpireAt = DateTime.UtcNow.AddDays(1).ToEpochDate() });
            await dbContext.SaveChangesAsync();

            // Act
            Commit(dbContext, x => x.PersistHash("hash-1"));
            dbContext.ChangeTracker.Clear();
            // Assert

            var records = dbContext.Hashes
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.Null(records["hash-1"]);
            Assert.NotNull(records["hash-2"]);
        });
    }

    [Fact]
    public async Task PersistHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.PersistHash(null)));
            //Assert

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task PersistJob_ClearsTheJobExpirationData()
    {
        await UseDbContext(async (dbContext) =>
        {
            //Arrange
            var insertTwoResult = await InsertTwoJobs(dbContext,
                item => { item.ExpireAt = item.CreatedAt = DateTime.UtcNow.ToEpochDate(); });

            Commit(dbContext, x => x.PersistJob(insertTwoResult.JobId1));

            //Act
            dbContext.ChangeTracker.Clear();
            var job = await GetTestJob(dbContext, insertTwoResult.JobId1);
            //Assert
            Assert.Null(job.ExpireAt);

            var anotherJob = await GetTestJob(dbContext, insertTwoResult.JobId2);
            Assert.NotNull(anotherJob.ExpireAt);
        });
    }

    [Fact]
    public async Task PersistList_ClearsExpirationTime_OnAGivenHash()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _List { Key = "list-1", ExpireAt = DateTime.UtcNow.AddDays(-1).ToEpochDate() });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _List { Key = "list-2", ExpireAt = DateTime.UtcNow.AddDays(-1).ToEpochDate() });
            await dbContext.SaveChangesAsync();
            //does nothing
            // Act
            Commit(dbContext, x => x.PersistList("list-1"));
            dbContext.ChangeTracker.Clear();
            // Assert

            var records = dbContext.Lists
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.Null(records["list-1"]);
            Assert.NotNull(records["list-2"]);
        });
    }

    [Fact]
    public async Task PersistList_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.PersistList(null)));
            //Assert

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task PersistSet_ClearsExpirationTime_OnAGivenHash()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _Set { Key = "set-1", Value = "1", ExpireAt = DateTime.UtcNow.AddDays(-1).ToEpochDate() });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Set { Key = "set-2", Value = "1", ExpireAt = DateTime.UtcNow.AddDays(-1).ToEpochDate() });
            await dbContext.SaveChangesAsync();

            // Act
            Commit(dbContext, x => x.PersistSet("set-1"));
            dbContext.ChangeTracker.Clear();
            // Assert
            var records = dbContext.Sets
                .ToDictionary(x => x.Key, x => x.ExpireAt);
            Assert.Null(records["set-1"]);
            Assert.NotNull(records["set-2"]);
        });
    }

    [Fact]
    public async Task PersistSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.PersistSet(null)));
            //Assert

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task RemoveFromList_DoesNotRemoveRecords_WithSameKey_ButDifferentValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "my-value");
                x.RemoveFromList("my-key", "different-value");
            });

            //Act

            //Assert
            Assert.Single(dbContext.Lists);
        });
    }

    [Fact]
    public async Task RemoveFromList_DoesNotRemoveRecords_WithSameValue_ButDifferentKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "my-value");
                x.RemoveFromList("different-key", "my-value");
            });
            //Act

            var recordCount = dbContext.Lists.Count();
            //Assert
            Assert.Equal(1, recordCount);
        });
    }

    [Fact]
    public async Task RemoveFromList_RemovesAllRecords_WithGivenKeyAndValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "my-value");
                x.InsertToList("my-key", "my-value");
                x.RemoveFromList("my-key", "my-value");
            });
            //Act

            //Assert
            Assert.Empty(dbContext.Lists);
        });
    }

    [Fact]
    public async Task RemoveFromSet_DoesNotRemoveRecord_WithSameKey_AndDifferentValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.RemoveFromSet("my-key", "different-value");
            });
            //Act

            //Assert
            Assert.Single(dbContext.Sets);
        });
    }

    [Fact]
    public async Task RemoveFromSet_DoesNotRemoveRecord_WithSameValue_AndDifferentKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.RemoveFromSet("different-key", "my-value");
            });
            //Act

            //Assert
            Assert.Single(dbContext.Sets);
        });
    }

    [Fact]
    public async Task RemoveFromSet_RemovesARecord_WithGivenKeyAndValue()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Arrange
            Commit(dbContext, x =>
            {
                x.AddToSet("my-key", "my-value");
                x.RemoveFromSet("my-key", "my-value");
            });
            //Act

            //Assert
            Assert.Empty(dbContext.Sets);
        });
    }

    [Fact]
    public async Task RemoveHash_RemovesAllHashRecords()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange
            Commit(dbContext, x => x.SetRangeInHash("some-hash", new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }));

            // Act
            Commit(dbContext, x => x.RemoveHash("some-hash"));

            // Assert
            Assert.Empty(dbContext.Hashes);
        });
    }

    [Fact]
    public async Task RemoveHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Assert
            Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.RemoveHash(null)));
        });
    }

    [Fact]
    public async Task RemoveSet_RemovesASet_WithAGivenKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            // Arrange
            dbContext.Add(new _Set { Key = "set-1", Value = "1" });
            dbContext.Add(new _Set { Key = "set-2", Value = "1" });
            await dbContext.SaveChangesAsync();


            Commit(dbContext, x => x.RemoveSet("set-1"));
            // Act

            var record = dbContext.Sets.Single();
            //Assert
            Assert.Equal("set-2", record.Key);
        });
    }

    [Fact]
    public async Task RemoveSet_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            //Assert

            Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.RemoveSet(null)));
        });
    }

    [Fact]
    public async Task SetJobState_AppendsAStateAndSetItToTheJob()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            // Arrange
            var insertTwoResult = await InsertTwoJobs(dbContext);

            var state = new Mock<IState>();
            const string expected = "State";
            state.Setup(x => x.Name).Returns(expected);
            const string reason = "Reason";
            state.Setup(x => x.Reason).Returns(reason);
            state.Setup(x => x.SerializeData())
                .Returns(new Dictionary<string, string> { { "Name", "Value" } });

            Commit(dbContext, x => x.SetJobState(insertTwoResult.JobId1, state.Object));
            // Act
            dbContext.ChangeTracker.Clear();
            var job = await GetTestJob(dbContext, insertTwoResult.JobId1);
            //Assert
            Assert.Equal(expected, job.StateName);


            var anotherJob = await GetTestJob(dbContext, insertTwoResult.JobId2);
            Assert.Null(anotherJob.StateName);


            var jobState = dbContext.JobStates.Single();
            Assert.Equal(insertTwoResult.JobId1, jobState.Job.Id);
            Assert.Equal(expected, jobState.Name);
            Assert.Equal(reason, jobState.Reason);
            Assert.InRange(connection.Storage.UtcNow.Subtract(jobState.CreatedAt.FromEpochDate()).TotalSeconds, -3, 10);
            Assert.Equal("{\"Name\":\"Value\"}", jobState.Data);
        });
    }

    [Fact]
    public async Task SetRangeInHash_MergesAllRecords()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange
            Commit(dbContext, x => x.SetRangeInHash("some-hash", new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }));
            // Act

            //Assert
            var result = dbContext.Hashes
                .Where(i => i.Key == "some-hash")
                .ToDictionary(x => x.Name, x => x.Value);

            Assert.Equal("Value1", result["Key1"]);
            Assert.Equal("Value2", result["Key2"]);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ThrowsAnException_WhenKeyIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.SetRangeInHash(null, new Dictionary<string, string>())));
            //Assert

            Assert.Equal("key", exception.ParamName);
        });
    }

    [Fact]
    public async Task SetRangeInHash_ThrowsAnException_WhenKeyValuePairsArgumentIsNull()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            var exception = Assert.Throws<ArgumentNullException>(
                () => Commit(dbContext, x => x.SetRangeInHash("some-hash", null)));
            //Assert
            Assert.Equal("keyValuePairs", exception.ParamName);
        });
    }

    [Fact]
    public async Task TrimList_RemovesAllRecords_IfStartFromGreaterThanEndingAt()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "0");
                x.TrimList("my-key", 1, 0);
            });
            // Act

           
            
            //Assert
            Assert.Empty(dbContext.Lists);
            
        });
    }

    [Fact]
    public async Task TrimList_RemovesAllRecords_WhenStartingFromValue_GreaterThanMaxElementIndex()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange
            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "0");
                x.TrimList("my-key", 1, 100);
            });
            // Act
             
            //Assert
            Assert.Empty(dbContext.Lists);
        });
    }

    [Fact]
    public async Task TrimList_RemovesRecords_OnlyOfAGivenKey()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange

            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "0");
                x.TrimList("another-key", 1, 0);
            });
            // Act
             
            //Assert
            Assert.Single(dbContext.Lists);
        });
    }

    [Fact]
    public async Task TrimList_RemovesRecordsToEnd_IfKeepAndingAt_GreaterThanMaxElementIndex()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange

            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "0");
                x.InsertToList("my-key", "1");
                x.InsertToList("my-key", "2");
                x.TrimList("my-key", 1, 100);
            });
            // Act


            var recordCount = dbContext.Lists.Count();
            //Assert
            Assert.Equal(2, recordCount);
        });
    }

    [Fact]
    public async Task TrimList_TrimsAList_ToASpecifiedRange()
    {
        await UseDbContext(async (dbContext) =>
        {
            await Task.CompletedTask;
            // Arrange

            Commit(dbContext, x =>
            {
                x.InsertToList("my-key", "0");
                x.InsertToList("my-key", "1");
                x.InsertToList("my-key", "2");
                x.InsertToList("my-key", "3");
                x.TrimList("my-key", 1, 2);
            });
            // Act
            dbContext.ChangeTracker.Clear();

            var records = dbContext.Lists.OrderBy(i => i.Id).ToArray();
            //Assert
            Assert.Equal(2, records.Length);
            Assert.Equal("1", records[0].Value);
            Assert.Equal("2", records[1].Value);
        });
    }


    private class InsertTwoJobsResult
    {
        public string? JobId1 { get; set; }
        public string? JobId2 { get; set; }
    }
}