using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc;

[Collection(Constants.SqlServerFixtureCollectionName)]
public class
    SqlServerStorageConnectionTests : StorageConnectionTestsBase
{
    public SqlServerStorageConnectionTests(SqlServerTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture, testOutputHelper)
    {
    }
}