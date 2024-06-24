using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _Set : KeyValueTypeBase<string>, IKeyWithStringValue, IStringValue
{
    public virtual double Score { get; set; }
}