using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;

public abstract class JobQueueTestsBase : TestBase
{
    private static readonly string[] DefaultQueues = { "default" };

    protected JobQueueTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
    }


    private static CancellationToken CreateTimingOutCancellationToken()
    {
        var source = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return source.Token;
    }

    private static CancellationToken CreateLongTimingOutCancellationToken()
    {
        var source = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        return source.Token;
    }


    private static EntityFrameworkJobQueue CreateJobQueue(EntityFrameworkJobStorage storage)
    {
        return new EntityFrameworkJobQueue(storage);
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenStorageIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkJobQueue(null));

        Assert.Equal("storage", exception.ParamName);
    }

    [Fact]
    public async Task Dequeue_ShouldDeleteAJob()
    {
        // Arrange
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await dbContext.DeleteAllAsync<_JobQueue>();
            await dbContext.DeleteAllAsync<_Job>();
            var newjob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobQueue { Job = newjob, Queue = "default" });
            await dbContext.SaveChangesAsync();
            //does nothing
            var queue = CreateJobQueue(connection.Storage);

            // Act
            var payload = queue.Dequeue(
                DefaultQueues,
                CreateLongTimingOutCancellationToken());

            payload.RemoveFromQueue();

            // Assert
            Assert.NotNull(payload);

            var jobInQueue = dbContext.JobQueues.SingleOrDefault();
            Assert.Null(jobInQueue);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldFetchAJob_FromTheSpecifiedQueue()
    {
        // Arrange
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            var newJob = await InsertNewJob(dbContext);
            var newJobQueue = new _JobQueue { Job = newJob, Queue = "default" };
            dbContext.Add(newJobQueue);
            await dbContext.SaveChangesAsync();


            var queue = CreateJobQueue(connection.Storage);

            // Act
            var payload = (EntityFrameworkFetchedJob)queue.Dequeue(
                DefaultQueues,
                CreateTimingOutCancellationToken());

            // Assert
            Assert.Equal(newJob.Id, payload.JobId);
            Assert.Equal("default", payload.Queue);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldFetchATimedOutJobs_FromTheSpecifiedQueue()
    {
        // Arrange
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            var newJob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobQueue
            {
                Job = newJob,
                FetchedAt = connection.Storage.UtcNow.AddDays(-1).ToEpochDate(),
                Queue = "default"
            });
            await dbContext.SaveChangesAsync();
            //does nothing
            var queue = CreateJobQueue(connection.Storage);

            // Act
            var payload = queue.Dequeue(
                DefaultQueues,
                CreateLongTimingOutCancellationToken());

            // Assert
            Assert.NotEmpty(payload.JobId);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldFetchJobs_FromMultipleQueues()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            var queueNames = new[] { "critical", "default" };
            foreach (var queueName in queueNames)
            {
                var newJob = await InsertNewJob(dbContext);
                dbContext.Add(new _JobQueue
                {
                    Job = newJob,
                    Queue = queueName
                });
                await dbContext.SaveChangesAsync();
            }

            //does nothing


            var queue = CreateJobQueue(connection.Storage);


            var critical = (EntityFrameworkFetchedJob)queue.Dequeue(
                queueNames,
                CreateLongTimingOutCancellationToken());

            Assert.NotNull(critical.JobId);
            Assert.Equal("critical", critical.Queue);

            var @default = (EntityFrameworkFetchedJob)queue.Dequeue(
                queueNames,
                CreateLongTimingOutCancellationToken());

            Assert.NotNull(@default.JobId);
            Assert.Equal("default", @default.Queue);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldFetchJobs_OnlyFromSpecifiedQueues()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await dbContext.DeleteAllAsync<_JobQueue>();
            await dbContext.DeleteAllAsync<_Job>();
            var newJob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobQueue
            {
                Job = newJob,
                Queue = "critical"
            });
            await dbContext.SaveChangesAsync();
            //does nothing

            var queue = CreateJobQueue(connection.Storage);

            Assert.Throws<OperationCanceledException>(
                () => queue.Dequeue(
                    DefaultQueues,
                    CreateTimingOutCancellationToken()));
        });
    }

    [Fact]
    public async Task Dequeue_ShouldSetFetchedAt_OnlyForTheFetchedJob()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            // Arrange
            await dbContext.DeleteAllAsync<_JobQueue>();
            await dbContext.DeleteAllAsync<_Job>();
            for (var i = 0; i < 2; i++)
            {
                var newJob = await InsertNewJob(dbContext);
                dbContext.Add(new _JobQueue
                {
                    Job = newJob,
                    Queue = "default"
                });
                await dbContext.SaveChangesAsync();
            }

            //does nothing

            var queue = CreateJobQueue(connection.Storage);

            // Act
            var payload = queue.Dequeue(
                DefaultQueues,
                CreateTimingOutCancellationToken());

            // Assert
            var otherJobFetchedAt = dbContext.JobQueues
                .Where(i => i.Job.Id != payload.JobId)
                .Select(i => i.FetchedAt)
                .Single();

            Assert.Null(otherJobFetchedAt);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldThrowAnException_WhenQueuesCollectionIsEmpty()
    {
        await UseJobStorageConnection(async (connection) =>
        {
            await Task.CompletedTask;
            var queue = CreateJobQueue(connection.Storage);

            var exception = Assert.Throws<ArgumentException>(
                () => queue.Dequeue(Array.Empty<string>(), CreateTimingOutCancellationToken()));

            Assert.Equal("queues", exception.ParamName);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldThrowAnException_WhenQueuesCollectionIsNull()
    {
        await UseJobStorageConnection(async (connection) =>
        {
            await Task.CompletedTask;
            var queue = CreateJobQueue(connection.Storage);

            var exception = Assert.Throws<ArgumentNullException>(
                () => queue.Dequeue(null, CreateTimingOutCancellationToken()));

            Assert.Equal("queues", exception.ParamName);
        });
    }

    [Fact]
    public async Task Dequeue_ShouldWaitIndefinitely_WhenThereAreNoJobs()
    {
        await UseJobStorageConnection(async (connection) =>
        {
            await Task.CompletedTask;
            var cts = new CancellationTokenSource(200);
            var queue = CreateJobQueue(connection.Storage);

            Assert.Throws<OperationCanceledException>(
                () => queue.Dequeue(DefaultQueues, cts.Token));
        });
    }

    [Fact]
    public async Task Dequeue_ThrowsOperationCanceled_WhenCancellationTokenIsSetAtTheBeginning()
    {
        await UseJobStorageConnection(async (connection) =>
        {
            await Task.CompletedTask;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var queue = CreateJobQueue(connection.Storage);

            Assert.Throws<OperationCanceledException>(
                () => queue.Dequeue(DefaultQueues, cts.Token));
        });
    }

    [Fact]
    public async Task Enqueue_AddsAJobToTheQueue()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            await dbContext.DeleteAllAsync<_JobQueue>();
            //does nothing

            var newJob = await InsertNewJob(dbContext);

            var queue = CreateJobQueue(connection.Storage);

            queue.Enqueue(dbContext, "default", newJob.Id);

            var record = dbContext.JobQueues.Single();
            Assert.Equal(newJob.Id, record.Job.Id);
            Assert.Equal("default", record.Queue);
            Assert.Null(record.FetchedAt);
        });
    }
}