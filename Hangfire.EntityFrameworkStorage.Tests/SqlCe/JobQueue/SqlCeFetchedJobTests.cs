using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.JobQueue
{
    [Collection(Constants.SqlCeFixtureCollectionName)]
 
    public class
        SqlCeFetchedJobTests : FetchedJobTestsBase
    {
        public SqlCeFetchedJobTests(SqlCeTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}