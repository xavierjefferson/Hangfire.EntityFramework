using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests;

public abstract class StorageOptionsTests
{
    [Fact]
    public async Task Ctor_SetsTheDefaultOptions()
    {
        await Task.CompletedTask;
        var options = new EntityFrameworkStorageOptions();

        Assert.True(options.QueuePollInterval > TimeSpan.Zero);

        Assert.True(options.JobExpirationCheckInterval > TimeSpan.Zero);
        Assert.True(options.UpdateSchema);
    }

    [Fact]
    public async Task Set_QueuePollInterval_SetsTheValue()
    {
        await Task.CompletedTask;
        var options = new EntityFrameworkStorageOptions();
        options.QueuePollInterval = TimeSpan.FromSeconds(1);
        Assert.Equal(TimeSpan.FromSeconds(1), options.QueuePollInterval);
    }

    [Fact]
    public async Task Set_QueuePollInterval_ShouldThrowAnException_WhenGivenIntervalIsEqualToZero()
    {
        await Task.CompletedTask;
        var options = new EntityFrameworkStorageOptions();
        Assert.Throws<ArgumentException>(
            () => options.QueuePollInterval = TimeSpan.Zero);
    }

    [Fact]
    public async Task Set_QueuePollInterval_ShouldThrowAnException_WhenGivenIntervalIsNegative()
    {
        await Task.CompletedTask;
        var options = new EntityFrameworkStorageOptions();
        Assert.Throws<ArgumentException>(
            () => options.QueuePollInterval = TimeSpan.FromSeconds(-1));
    }
}