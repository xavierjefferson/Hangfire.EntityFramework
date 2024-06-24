using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Misc
{
    [Xunit.Collection(Constants.MySqlFixtureCollectionName)]
    public class
        MySqlWriteOnlyTransactionTests : WriteOnlyTransactionTestsBase
    {
        public MySqlWriteOnlyTransactionTests(MySqlTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}