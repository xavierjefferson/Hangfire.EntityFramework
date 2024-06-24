using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Misc;

[Collection(Constants.MySqlFixtureCollectionName)]
public class
    MySqlWriteOnlyTransactionTests : WriteOnlyTransactionTestsBase
{
    public MySqlWriteOnlyTransactionTests(MySqlTestDatabaseFixture fixture) : base(fixture)
    {
    }
}