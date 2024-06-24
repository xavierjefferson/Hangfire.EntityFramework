using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Misc;

[Collection(Constants.MySqlFixtureCollectionName)]
public class MySqlExpirationManagerTest : ExpirationManagerTestsBase
{
    public MySqlExpirationManagerTest(MySqlTestDatabaseFixture fixture) : base(fixture)
    {
    }
}