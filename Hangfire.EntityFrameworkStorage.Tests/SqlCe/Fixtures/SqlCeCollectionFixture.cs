using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures
{
    [CollectionDefinition(Constants.SqlCeFixtureCollectionName)]
    public class SqlCeCollectionFixture : ICollectionFixture<SqlCeTestDatabaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}