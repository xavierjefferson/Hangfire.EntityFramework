using System;

namespace Hangfire.EntityFrameworkStorage.Interfaces;

public interface IFetchedAtNullable
{
    long? FetchedAt { get; set; }
}