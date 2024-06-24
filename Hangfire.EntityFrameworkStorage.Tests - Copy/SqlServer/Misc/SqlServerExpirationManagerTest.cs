using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class SqlServerExpirationManagerTest : ExpirationManagerTestsBase
    {
        public SqlServerExpirationManagerTest(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}