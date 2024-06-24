using Hangfire.EntityFrameworkStorage.JobQueue;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue;

public abstract class PersistentJobQueueProviderCollectionTests
{
    private static readonly string[] _queues = { "default", "critical" };
    private readonly Mock<IPersistentJobQueueProvider> _defaultProvider;
    private readonly Mock<IPersistentJobQueueProvider> _provider;

    public PersistentJobQueueProviderCollectionTests()
    {
        _defaultProvider = new Mock<IPersistentJobQueueProvider>();
        _provider = new Mock<IPersistentJobQueueProvider>();
    }

    private PersistentJobQueueProviderCollection CreateCollection()
    {
        return new PersistentJobQueueProviderCollection(_defaultProvider.Object);
    }

    [Fact]
    public async Task Add_ThrowsAnException_WhenProviderIsNull()
    {
        await Task.CompletedTask;
        var collection = CreateCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => collection.Add(null, _queues));

        Assert.Equal("provider", exception.ParamName);
    }

    [Fact]
    public async Task Add_ThrowsAnException_WhenQueuesCollectionIsNull()
    {
        await Task.CompletedTask;
        var collection = CreateCollection();

        var exception = Assert.Throws<ArgumentNullException>(
            () => collection.Add(_provider.Object, null));

        Assert.Equal("queues", exception.ParamName);
    }

    [Fact]
    public async Task Ctor_ThrowsAnException_WhenDefaultProviderIsNull()
    {
        await Task.CompletedTask;
        Assert.Throws<ArgumentNullException>(
            () => new PersistentJobQueueProviderCollection(null));
    }

    [Fact]
    public async Task Enumeration_ContainsAddedProvider()
    {
        await Task.CompletedTask;
        var collection = CreateCollection();

        collection.Add(_provider.Object, _queues);

        Assert.Contains(_provider.Object, collection);
    }

    [Fact]
    public async Task Enumeration_IncludesTheDefaultProvider()
    {
        var collection = CreateCollection();

        var result = collection.ToArray();

        Assert.Single(result);
        Assert.Same(_defaultProvider.Object, result[0]);
    }

    [Fact]
    public async Task GetProvider_CanBeResolved_ByAnyQueue()
    {
        await Task.CompletedTask;
        var collection = CreateCollection();
        collection.Add(_provider.Object, _queues);

        var provider1 = collection.GetProvider("default");
        var provider2 = collection.GetProvider("critical");

        Assert.NotSame(_defaultProvider.Object, provider1);
        Assert.Same(provider1, provider2);
    }

    [Fact]
    public async Task GetProvider_ReturnsTheDefaultProvider_WhenProviderCanNotBeResolvedByQueue()
    {
        await Task.CompletedTask;
        var collection = CreateCollection();

        var provider = collection.GetProvider("queue");

        Assert.Same(_defaultProvider.Object, provider);
    }
}