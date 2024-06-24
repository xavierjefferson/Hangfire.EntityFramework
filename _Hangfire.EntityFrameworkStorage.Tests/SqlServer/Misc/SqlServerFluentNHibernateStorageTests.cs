using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class SqlServerEntityFrameworkStorageTests : EntityFrameworkStorageTests
    {
        public SqlServerEntityFrameworkStorageTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}