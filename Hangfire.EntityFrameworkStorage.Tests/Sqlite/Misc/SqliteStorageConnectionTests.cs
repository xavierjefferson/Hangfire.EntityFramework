using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.Misc;

[Collection(Constants.SqliteFixtureCollectionName)]
public class
    SqliteStorageConnectionTests : StorageConnectionTestsBase
{
    public SqliteStorageConnectionTests(SqliteTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) : base(
        fixture, testOutputHelper)
    {
    }
}