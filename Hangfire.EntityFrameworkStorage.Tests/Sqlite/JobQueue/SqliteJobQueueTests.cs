using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.JobQueue
{
    [Xunit.Collection(Constants.SqliteFixtureCollectionName)]
    public class SqliteJobQueueTests : JobQueueTestsBase
    {
        public SqliteJobQueueTests(SqliteTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}