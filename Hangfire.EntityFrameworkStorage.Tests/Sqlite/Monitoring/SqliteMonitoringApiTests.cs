using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.Monitoring;

[Collection(Constants.SqliteFixtureCollectionName)]
public class SqliteMonitoringApiTests : MonitoringApiTestsBase
{
    public SqliteMonitoringApiTests(SqliteTestDatabaseFixture fixture) : base(fixture)
    {
    }
}