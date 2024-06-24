using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class EntityFrameworkStorageTests : TestBase
{
    public EntityFrameworkStorageTests(DatabaseFixtureBase fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenPersistenceConfigurerIsNull()
    {
        await Task.CompletedTask;
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EntityFrameworkJobStorage(null));

        Assert.Equal("dbContextOptionsBuilder", exception.ParamName, StringComparer.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task GetComponents_ReturnsAllNeededComponents()
    {
        await Task.CompletedTask;
        var storage = GetStorage();

        var components = storage.GetBackgroundProcesses();
        Assert.True(components.OfType<ExpirationManager>().Any());
        Assert.True(components.OfType<ServerTimeSyncManager>().Any());
        Assert.True(components.OfType<CountersAggregator>().Any());
    }


    [Fact]
    public async Task GetConnection_ReturnsNonNullInstance()
    {
        await Task.CompletedTask;
        var storage = GetStorage();

        using (var connection = storage.GetConnection())
        {
            Assert.NotNull(connection);
        }
    }

    [Fact]
    public async Task GetMonitoringApi_ReturnsNonNullInstance()
    {
        await Task.CompletedTask;
        var storage = GetStorage();

        var api = storage.GetMonitoringApi();
        Assert.NotNull(api);
    }
}