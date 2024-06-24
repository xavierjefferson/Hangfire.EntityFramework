using Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;
using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.MySql.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.MySql.JobQueue;

[Collection(Constants.MySqlFixtureCollectionName)]
public class
    MySqlJobQueueTests : JobQueueTestsBase
{
    public MySqlJobQueueTests(MySqlTestDatabaseFixture fixture) : base(fixture)
    {
    }
}