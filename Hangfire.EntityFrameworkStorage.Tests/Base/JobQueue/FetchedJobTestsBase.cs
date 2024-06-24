using Hangfire.EntityFrameworkStorage.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;

public abstract class FetchedJobTestsBase : TestBase
{
    private const string Queue = "queue";

    private static readonly string JobId = Guid.NewGuid().ToString();
    private readonly FetchedJob _fetchedJob;
    private readonly int _id = 0;

    protected FetchedJobTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
        _fetchedJob = new FetchedJob { Id = _id, JobId = JobId, Queue = Queue };
    }


    [Fact]
    public async Task Ctor_CorrectlySets_AllInstanceProperties()
    {
        await Task.CompletedTask;
        var fetchedJob = new EntityFrameworkFetchedJob(GetStorage(), _fetchedJob);

        Assert.Equal(JobId, fetchedJob.JobId);
        Assert.Equal(Queue, fetchedJob.Queue);
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenConnectionIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkFetchedJob(null, _fetchedJob));

        Assert.Equal("storage", exception.ParamName);
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenStorageIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkFetchedJob(null, _fetchedJob));

        Assert.Equal("storage", exception.ParamName);
    }
}