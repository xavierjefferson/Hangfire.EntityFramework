using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.Misc;

[Collection(Constants.SqliteFixtureCollectionName)]
public class
    SqliteWriteOnlyTransactionTests : WriteOnlyTransactionTestsBase
{
    public SqliteWriteOnlyTransactionTests(SqliteTestDatabaseFixture fixture) : base(fixture)
    {
    }
}