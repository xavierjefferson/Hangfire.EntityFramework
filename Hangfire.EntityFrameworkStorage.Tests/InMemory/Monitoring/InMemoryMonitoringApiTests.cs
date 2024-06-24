using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Base.Monitoring;
using Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.Monitoring
{
    [Collection(Constants.InMemoryFixtureCollectionName)]
    public class InMemoryMonitoringApiTests : MonitoringApiTestsBase
    {
        public InMemoryMonitoringApiTests(InMemoryTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}