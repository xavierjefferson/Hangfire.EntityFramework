using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.Misc
{
    [Xunit.Collection(Constants.MySqlFixtureCollectionName)]
    public class MySqlCounterAggregatorTests : CountersAggregatorTestsBase
    {
        public MySqlCounterAggregatorTests(MySqlTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}