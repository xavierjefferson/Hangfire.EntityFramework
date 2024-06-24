namespace Hangfire.EntityFrameworkStorage.Interfaces;

internal interface IKeyWithStringValue
{
    string Key { get; }
    string Value { get; }
}