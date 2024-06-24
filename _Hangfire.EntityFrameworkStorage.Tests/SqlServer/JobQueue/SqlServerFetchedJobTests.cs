using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.JobQueue
{
    [Collection(Constants.SqlServerFixtureCollectionName)]
    public class
        SqlServerFetchedJobTests : FetchedJobTestsBase
    {
        public SqlServerFetchedJobTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}