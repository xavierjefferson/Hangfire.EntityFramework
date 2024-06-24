using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Hangfire.Server;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class ExpirationManagerTestsBase : TestBase
{
    protected ExpirationManagerTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
    }

    public override EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions? options = null)
    {
        var tmp = base.GetStorage(options);
        tmp.Options.JobExpirationCheckInterval = TimeSpan.Zero;
        return tmp;
    }

    private static async Task<long> CreateExpirationEntry(HangfireContext dbContext, DateTime? expireAt)
    {
        await dbContext.DeleteAllAsync<_AggregatedCounter>();
        var aggregatedCounter = new _AggregatedCounter { Key = "key", Value = 1, ExpireAt = expireAt.ToEpochDate() };
        dbContext.Add(aggregatedCounter);
        await dbContext.SaveChangesAsync();

        return aggregatedCounter.Id;
    }

    private static async Task<bool> IsEntryExpired(HangfireContext dbContext, long entryId)
    {
        await Task.CompletedTask;
        return !dbContext.AggregatedCounters.Any(i => i.Id == entryId);
    }


    private TestInfo GetTestInfo(EntityFrameworkJobStorage storage)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var result = new TestInfo
        {
            Manager = new ExpirationManager(storage),
            BackgroundProcessContext = new BackgroundProcessContext("dummy", storage,
                new Dictionary<string, object>(), Guid.NewGuid(),
                cancellationTokenSource.Token, cancellationTokenSource.Token, cancellationTokenSource.Token)
        };

        return result;
    }


    [Fact]
    public async Task Ctor_ThrowsAnException_WhenStorageIsNull()
    {
        await Task.CompletedTask;
        Assert.Throws<ArgumentNullException>(() => new ExpirationManager(null));
    }

    [Fact]
    public async Task Execute_DoesNotRemoveEntries_WithFreshExpirationTime()
    {
        await UseDbContext(async dbContext =>
        {
            //Arrange
            var entryId = await CreateExpirationEntry(dbContext, DateTime.UtcNow.AddMonths(1));
            var testInfo = GetTestInfo(GetStorage());

            //Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            //Assert
            Assert.False(await IsEntryExpired(dbContext, entryId));
        });
    }

    [Fact]
    public async Task Execute_DoesNotRemoveEntries_WithNoExpirationTimeSet()
    {
        await UseDbContext(async dbContext =>
        {
            //Arrange
            var entryId = await CreateExpirationEntry(dbContext, null);
            var testInfo = GetTestInfo(GetStorage());

            //Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            //Assert
            Assert.False(await IsEntryExpired(dbContext, entryId));
        });
    }

    [Fact]
    public async Task Execute_Processes_AggregatedCounterTable()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            dbContext.Add(new _AggregatedCounter
            {
                Key = "key",
                Value = 1,
                ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();

            var testInfo = GetTestInfo(GetStorage());

            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            // Assert
            Assert.Empty(dbContext.Counters);
        });
    }

    [Fact]
    public async Task Execute_Processes_HashTable()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            dbContext.Add(new _Hash
            {
                Key = "key1",
                Name = "field",
                Value = string.Empty,
                ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();
            dbContext.Add(new _Hash
            {
                Key = "key2",
                Name = "field",
                Value = string.Empty,
                ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            var testInfo = GetTestInfo(GetStorage());

            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            // Assert
            Assert.Empty(dbContext.Hashes);
        });
    }

    [Fact]
    public async Task Execute_Processes_JobTable()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            dbContext.Add(new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                CreatedAt = DateTime.UtcNow.ToEpochDate(),
                ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();


            var testInfo = GetTestInfo(GetStorage());

            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            // Assert
            Assert.Empty(dbContext.Jobs);
        });
    }

    [Fact]
    public async Task Execute_Processes_ListTable()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            dbContext.Add(new _List { Key = "key", ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate() });
            await dbContext.SaveChangesAsync();


            var testInfo = GetTestInfo(GetStorage());

            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            // Assert
            Assert.Empty(dbContext.Lists);
        });
    }

    [Fact]
    public async Task Execute_Processes_SetTable()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            dbContext.Add(new _Set
            {
                Key = "key",
                Score = 0,
                Value = string.Empty,
                ExpireAt = DateTime.UtcNow.AddMonths(-1).ToEpochDate()
            });
            await dbContext.SaveChangesAsync();


            var testInfo = GetTestInfo(GetStorage());

            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);

            // Assert
            Assert.Empty(dbContext.Sets);
        });
    }

    [Fact]
    public async Task Execute_RemovesOutdatedRecords()
    {
        await UseDbContext(async dbContext =>
        {
            // Arrange
            var entryId = await CreateExpirationEntry(dbContext, DateTime.UtcNow.AddMonths(-1));
            var testInfo = GetTestInfo(GetStorage());
            // Act
            testInfo.Manager?.Execute(testInfo.BackgroundProcessContext);
            //Assert
            Assert.True(await IsEntryExpired(dbContext, entryId));
        });
    }

    private class TestInfo
    {
        public BackgroundProcessContext? BackgroundProcessContext { get; set; }
        public ExpirationManager? Manager { get; set; }
    }
}