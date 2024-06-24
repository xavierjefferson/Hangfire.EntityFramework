using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class CountersAggregatorTestsBase : TestBase
{
    protected CountersAggregatorTestsBase(DatabaseFixtureBase fixture) : base(fixture)
    {
    }


    [Fact]
    public async Task CountersAggregatorExecutesProperly()
    {
        await UseJobStorageConnectionWithDbContext(async (dbContext, connection) =>
        {
            //Arrange
            dbContext.Add(new _Counter
            { Key = "key", Value = 1, ExpireAt = connection.Storage.UtcNow.AddHours(1).ToEpochDate() });
            await dbContext.SaveChangesAsync();


            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var countersAggregator = new CountersAggregator(connection.Storage);
            countersAggregator.Execute(cts.Token);

            // Assert
            Assert.Single(dbContext.AggregatedCounters);
        });
    }
}