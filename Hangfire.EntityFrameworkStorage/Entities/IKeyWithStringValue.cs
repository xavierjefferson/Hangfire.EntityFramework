namespace Hangfire.EntityFrameworkStorage.Entities;

internal interface IKeyWithStringValue
{
    string Key { get; }
    string Value { get; }
}