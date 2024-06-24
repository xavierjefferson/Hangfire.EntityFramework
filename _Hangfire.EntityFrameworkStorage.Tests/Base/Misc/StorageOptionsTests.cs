using System;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc
{
    public class StorageOptionsTests
    {
        [Fact]
        public void Ctor_SetsTheDefaultOptions()
        {
            var options = new EntityFrameworkStorageOptions();

            Assert.True(options.QueuePollInterval > TimeSpan.Zero);

            Assert.True(options.JobExpirationCheckInterval > TimeSpan.Zero);
            Assert.True(options.UpdateSchema);
        }

        [Fact]
        public void Set_QueuePollInterval_SetsTheValue()
        {
            var options = new EntityFrameworkStorageOptions();
            options.QueuePollInterval = TimeSpan.FromSeconds(1);
            Assert.Equal(TimeSpan.FromSeconds(1), options.QueuePollInterval);
        }

        [Fact]
        public void Set_QueuePollInterval_ShouldThrowAnException_WhenGivenIntervalIsEqualToZero()
        {
            var options = new EntityFrameworkStorageOptions();
            Assert.Throws<ArgumentException>(
                () => options.QueuePollInterval = TimeSpan.Zero);
        }

        [Fact]
        public void Set_QueuePollInterval_ShouldThrowAnException_WhenGivenIntervalIsNegative()
        {
            var options = new EntityFrameworkStorageOptions();
            Assert.Throws<ArgumentException>(
                () => options.QueuePollInterval = TimeSpan.FromSeconds(-1));
        }
    }
}