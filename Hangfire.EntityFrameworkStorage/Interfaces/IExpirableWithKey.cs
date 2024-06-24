namespace Hangfire.EntityFrameworkStorage.Interfaces;

internal interface IExpirableWithKey : IExpirable
{
    string Key { get; set; }
}