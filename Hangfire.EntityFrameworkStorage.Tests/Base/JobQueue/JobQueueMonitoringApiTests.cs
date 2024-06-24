using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;

public abstract class JobQueueMonitoringApiTests : TestBase, IDisposable

{
    private readonly EntityFrameworkJobQueueMonitoringApi _api;

    private readonly string _queue = "default";

    protected JobQueueMonitoringApiTests(DatabaseFixtureBase fixture) : base(fixture)
    {
        var storage = GetStorage();

        _api = new EntityFrameworkJobQueueMonitoringApi(storage);
    }

    [Fact]
    public async Task GetEnqueuedAndFetchedCount_ReturnsEqueuedCount_WhenExists()
    {
        EnqueuedAndFetchedCountDto? result = null;

        await UseDbContext(async (dbContext) =>
        {
            var newJob = await InsertNewJob(dbContext);
            dbContext.Add(new _JobQueue { Job = newJob, Queue = _queue });
            await dbContext.SaveChangesAsync();
            result = _api.GetEnqueuedAndFetchedCount(_queue);

            await dbContext.DeleteAllAsync<_JobQueue>();
        });

        Assert.Equal(1, result?.EnqueuedCount);
    }

    [Fact]
    public async Task GetEnqueuedJobIds_ReturnsCorrectResult()
    {
        string[]? result = null;
        var jobs = new List<_Job>();
        await UseDbContext(async (dbContext) =>
        {
            for (var i = 1; i <= 10; i++)
            {
                var newJob = await InsertNewJob(dbContext);
                jobs.Add(newJob);
                dbContext.Add(new _JobQueue { Job = newJob, Queue = _queue });
                await dbContext.SaveChangesAsync();
            }

            //does nothing
            result = _api.GetEnqueuedJobIds(_queue, 3, 2).ToArray();

            await dbContext.DeleteAllAsync<_JobQueue>();
        });

        Assert.Equal(2, result?.Length);
        Assert.Equal(jobs[3].Id, result?[0]);
        Assert.Equal(jobs[4].Id, result?[1]);
    }


    [Fact]
    public async Task GetEnqueuedJobIds_ReturnsEmptyCollection_IfQueueIsEmpty()
    {
        await Task.CompletedTask;
        var result = _api.GetEnqueuedJobIds(_queue, 5, 15);

        Assert.Empty(result);
    }
}