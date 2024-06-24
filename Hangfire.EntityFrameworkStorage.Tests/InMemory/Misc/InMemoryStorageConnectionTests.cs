using Hangfire.EntityFrameworkStorage.Tests.Base.Misc;
using Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.Misc
{
    [Collection(Constants.InMemoryFixtureCollectionName)]
    public class
        InMemoryStorageConnectionTests : StorageConnectionTestsBase
    {
        public InMemoryStorageConnectionTests(InMemoryTestDatabaseFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
        {
        }
    }
}