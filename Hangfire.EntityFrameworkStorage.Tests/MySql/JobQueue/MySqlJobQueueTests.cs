using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.JobQueue
{
    [Xunit.Collection(Constants.MySqlFixtureCollectionName)]
    public class
        MySqlJobQueueTests : JobQueueTestsBase
    {
        public MySqlJobQueueTests(MySqlTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}