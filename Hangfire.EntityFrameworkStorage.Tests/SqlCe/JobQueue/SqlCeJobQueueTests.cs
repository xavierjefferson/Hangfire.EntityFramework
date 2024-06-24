using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.JobQueue
{
    [Xunit.Collection(Constants.SqlCeFixtureCollectionName)]
    public class
        SqlCeJobQueueTests : JobQueueTestsBase
    {
        public SqlCeJobQueueTests(SqlCeTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}