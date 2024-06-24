using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _List : KeyValueTypeBase<string>, IKeyWithStringValue, IStringValue
{
}