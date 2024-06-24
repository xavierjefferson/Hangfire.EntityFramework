using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.JobQueue
{
    [Xunit.Collection(Constants.SqliteFixtureCollectionName)]
    public class SqliteFetchedJobTests : FetchedJobTestsBase
    {
        public SqliteFetchedJobTests(SqliteTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}