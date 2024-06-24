using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.JobQueue;

[Collection(Constants.InMemoryFixtureCollectionName)]
public class
    InMemoryJobQueueMonitoringApiTests : JobQueueMonitoringApiTests
{
    public InMemoryJobQueueMonitoringApiTests(InMemoryTestDatabaseFixture fixture) : base(fixture)
    {
    }
}