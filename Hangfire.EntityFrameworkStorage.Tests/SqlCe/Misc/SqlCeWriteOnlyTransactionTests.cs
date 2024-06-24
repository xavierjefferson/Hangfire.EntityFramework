using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.Misc
{
    [Xunit.Collection(Constants.SqlCeFixtureCollectionName)]
    public class
        SqlCeWriteOnlyTransactionTests : WriteOnlyTransactionTestsBase
    {
        public SqlCeWriteOnlyTransactionTests(SqlCeTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}