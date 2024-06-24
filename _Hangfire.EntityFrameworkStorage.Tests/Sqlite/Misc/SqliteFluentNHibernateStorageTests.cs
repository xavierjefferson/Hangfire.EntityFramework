using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.Misc
{
    [Collection(Constants.SqliteFixtureCollectionName)]
    public class SqliteEntityFrameworkStorageTests : EntityFrameworkStorageTests
    {
        public SqliteEntityFrameworkStorageTests(SqliteTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}