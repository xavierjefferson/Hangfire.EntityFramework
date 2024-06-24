using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.Misc
{
    [Collection(Constants.InMemoryFixtureCollectionName)]
    public class InMemoryEntityFrameworkStorageTests : EntityFrameworkStorageTests
    {
        public InMemoryEntityFrameworkStorageTests(InMemoryTestDatabaseFixture fixture) : base(fixture)
        {
        }
    }
}