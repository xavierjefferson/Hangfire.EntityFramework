using Hangfire.EntityFrameworkStorage.Extensions;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Misc;

public abstract class DateHelperTests
{
    [Fact]
    public async Task TestDateTimeToLong()
    {
        await Task.CompletedTask;
        var r = new Random();
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            var dateTimeValue = DateTimeOffset.FromUnixTimeSeconds(r.Next()).UtcDateTime;
            var longValue = dateTimeValue.ToEpochDate();
            var newDateTimeValue = longValue.FromEpochDate();
            Assert.Equal(newDateTimeValue, dateTimeValue);
        }
    }

    [Fact]
    public async Task TestLongToDateTime()
    {
        await Task.CompletedTask;
        var r = new Random();
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            var longValue = Convert.ToInt64(r.NextDouble() * 315537897599999) - 62135596800000;
            var dateTimeValue = longValue.FromEpochDate();
            var newLongValue = dateTimeValue.ToEpochDate();
            Assert.Equal(longValue, newLongValue);
        }
    }
}