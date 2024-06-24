using System;

namespace Hangfire.EntityFrameworkStorage.Extensions;

public static class DateHelper
{
    public static long ToEpochDate(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }
    public static long? ToEpochDate(this DateTime? dateTime)
    {
        if (dateTime == null) return null;
        return ToEpochDate(dateTime.Value);
    }

    public static DateTime FromEpochDate(this long dateTime)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(dateTime).UtcDateTime;
    }
    public static DateTime? FromEpochDate(this long? dateTime)
    {
        if (dateTime == null) return null;
        return FromEpochDate(dateTime.Value);
    }
}