using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Monitoring
{
    [Xunit.Collection(Constants.MySqlFixtureCollectionName)]
    public class
        MySqlMonitoringApiTests : MonitoringApiTestsBase
    {
        public MySqlMonitoringApiTests(MySqlTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}