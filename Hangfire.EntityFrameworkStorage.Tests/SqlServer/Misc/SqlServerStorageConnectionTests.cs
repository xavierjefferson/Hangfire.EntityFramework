using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class
        SqlServerStorageConnectionTests : StorageConnectionTestsBase
    {
        public SqlServerStorageConnectionTests(SqlServerTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
        {
        }
    }
}