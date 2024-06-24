using System;
using System.Linq;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc
{
    public abstract class EntityFrameworkStorageTests : TestBase
    {
        public EntityFrameworkStorageTests(DatabaseFixtureBase fixture) : base(fixture)
        {
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenInfoIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new EntityFrameworkJobStorage(null));

            Assert.Equal("info", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenPersistenceConfigurerIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new EntityFrameworkJobStorage(null, null));

            Assert.Equal("persistenceConfigurer", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
        }

        [Fact]
        public void GetComponents_ReturnsAllNeededComponents()
        {
            var storage = GetStorage();

            var components = storage.GetBackgroundProcesses();
            Assert.True(components.OfType<ExpirationManager>().Any());
            Assert.True(components.OfType<ServerTimeSyncManager>().Any());
            Assert.True(components.OfType<CountersAggregator>().Any());
        }


        [Fact]
        public void GetConnection_ReturnsNonNullInstance()
        {
            var storage = GetStorage();

            using (var connection = storage.GetConnection())
            {
                Assert.NotNull(connection);
            }
        }

        [Fact]
        public void GetMonitoringApi_ReturnsNonNullInstance()
        {
            var storage = GetStorage();

            var api = storage.GetMonitoringApi();
            Assert.NotNull(api);
        }
    }
}