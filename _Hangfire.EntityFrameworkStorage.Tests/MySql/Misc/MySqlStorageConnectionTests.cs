using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Misc
{
    [Xunit.Collection(Constants.MySqlFixtureCollectionName)]
    public class
        MySqlStorageConnectionTests : StorageConnectionTestsBase
    {
        public MySqlStorageConnectionTests(MySqlTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
        {
        }
    }
}