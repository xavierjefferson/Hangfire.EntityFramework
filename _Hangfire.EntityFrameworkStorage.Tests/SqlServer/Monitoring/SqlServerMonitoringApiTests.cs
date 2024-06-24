using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Monitoring
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class
        SqlServerMonitoringApiTests : MonitoringApiTestsBase
    {
        public SqlServerMonitoringApiTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}