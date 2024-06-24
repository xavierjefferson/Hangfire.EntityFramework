using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc;

[Collection(Constants.SqlServerFixtureCollectionName)]
public class SqlServerEntityFrameworkStorageTests : EntityFrameworkStorageTests
{
    public SqlServerEntityFrameworkStorageTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
    {
    }
}