using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.JobQueue
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class
        SqlServerJobQueueMonitoringApiTests : JobQueueMonitoringApiTests
    {
        public SqlServerJobQueueMonitoringApiTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}