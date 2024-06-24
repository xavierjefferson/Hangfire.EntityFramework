using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.Monitoring
{
    [Xunit.Collection(Constants.SqlCeFixtureCollectionName)]
    public class
        SqlCeMonitoringApiTests : MonitoringApiTestsBase
    {
        public SqlCeMonitoringApiTests(SqlCeTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}