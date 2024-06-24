using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.JobQueue
{
    [Xunit.Collection(Constants.InMemoryFixtureCollectionName)]
    public class InMemoryJobQueueTests : JobQueueTestsBase
    {
        public InMemoryJobQueueTests(InMemoryTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}