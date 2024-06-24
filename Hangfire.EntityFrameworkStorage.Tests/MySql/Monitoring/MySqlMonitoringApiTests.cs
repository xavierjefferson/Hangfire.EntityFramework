using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Monitoring;

[Collection(Constants.MySqlFixtureCollectionName)]
public class
    MySqlMonitoringApiTests : MonitoringApiTestsBase
{
    public MySqlMonitoringApiTests(MySqlTestDatabaseFixture fixture) : base(fixture)
    {
    }
}