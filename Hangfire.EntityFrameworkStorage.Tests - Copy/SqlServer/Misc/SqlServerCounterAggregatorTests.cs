using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.SqlServer.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.SqlServer.Misc
{
    [Xunit.Collection(Constants.SqlServerFixtureCollectionName)]
    public class SqlServerCounterAggregatorTests : CountersAggregatorTestsBase
    {
        public SqlServerCounterAggregatorTests(SqlServerTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}