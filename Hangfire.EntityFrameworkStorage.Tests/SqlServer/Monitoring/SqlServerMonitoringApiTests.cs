using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Monitoring;

[Collection(Constants.SqlServerFixtureCollectionName)]
public class
    SqlServerMonitoringApiTests : MonitoringApiTestsBase
{
    public SqlServerMonitoringApiTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
    {
    }
}