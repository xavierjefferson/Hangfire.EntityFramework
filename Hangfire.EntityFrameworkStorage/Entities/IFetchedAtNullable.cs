using System;

namespace Hangfire.EntityFrameworkStorage.Entities
{
    public interface IFetchedAtNullable
    {
        DateTime? FetchedAt { get; set; }
    }
}