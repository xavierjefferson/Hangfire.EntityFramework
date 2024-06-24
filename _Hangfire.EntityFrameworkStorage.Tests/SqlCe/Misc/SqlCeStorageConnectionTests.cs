using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlCe.Fixtures;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlCe.Misc
{
    [Xunit.Collection(Constants.SqlCeFixtureCollectionName)]
    public class
        SqlCeStorageConnectionTests : StorageConnectionTestsBase
    {
        public SqlCeStorageConnectionTests(SqlCeTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
        {
        }
    }
}