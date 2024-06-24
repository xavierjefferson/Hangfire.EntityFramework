using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.JobQueue;

[Collection(Constants.SqliteFixtureCollectionName)]
public class
    SqliteJobQueueMonitoringApiTests : JobQueueMonitoringApiTests
{
    public SqliteJobQueueMonitoringApiTests(SqliteTestDatabaseFixture fixture) : base(fixture)
    {
    }
}