﻿namespace Hangfire.EntityFrameworkStorage.Maps;

public class Defaults
{
    public const int DistributedLockPollIntervalSeconds = 15;
}

internal class Constants
{
    //exceeding 4000 will break sqlce
    public const int VarcharMaxLength = 4000;

    public const int StateReasonLength = 100;
    public const int StateDataLength = VarcharMaxLength;
    public const int StateNameLength = 20;

    public class ColumnNames
    {
        public static readonly string JobId = "JobId";
        public static readonly string Id = "Id";
        public static readonly string Data = "Data";
        public static readonly string CreatedAt = "CreatedAt";
        public static readonly string Key = "Key";
        public static readonly string Value = "Value";
    }
}